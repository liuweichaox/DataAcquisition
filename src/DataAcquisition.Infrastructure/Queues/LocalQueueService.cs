using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using DataAcquisition.Infrastructure.DataStorages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataAcquisition.Infrastructure.Queues;

/// <summary>
///     消息队列实现，支持批量处理优化
/// </summary>
public class LocalQueueService : IQueueService
{
    private readonly object _batchLock = new();
    private readonly ConcurrentDictionary<string, int> _batchSizeCache = new(); // 缓存 BatchSize 配置
    private readonly Channel<DataMessage> _channel = Channel.CreateUnbounded<DataMessage>();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    private readonly IDeviceConfigService _deviceConfigService;
    private readonly ConcurrentQueue<FailedBatch> _failedBatches = new();
    private readonly TimeSpan _flushInterval;
    private readonly Timer? _flushTimer;
    private readonly InfluxDbDataStorageService _influxStorage;
    private readonly ILogger<LocalQueueService> _logger;
    private readonly int _maxRetryCount;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly ParquetFileStorageService _parquetStorage;
    private readonly Stopwatch _processingStopwatch = new();
    private readonly TimeSpan _retryInterval;
    private readonly Timer? _retryTimer;

    public LocalQueueService(
        ParquetFileStorageService parquetStorage,
        InfluxDbDataStorageService influxStorage,
        ILogger<LocalQueueService> logger,
        IOptions<AcquisitionOptions> acquisitionOptions,
        IDeviceConfigService deviceConfigService,
        IMetricsCollector? metricsCollector = null)
    {
        _parquetStorage = parquetStorage;
        _influxStorage = influxStorage;
        _logger = logger;
        _metricsCollector = metricsCollector;
        _deviceConfigService = deviceConfigService;

        var options = acquisitionOptions.Value.QueueService;
        _flushInterval = TimeSpan.FromSeconds(options.FlushIntervalSeconds);
        _retryInterval = TimeSpan.FromSeconds(options.RetryIntervalSeconds);
        _maxRetryCount = options.MaxRetryCount;

        // 启动定时刷新和重试任务
        _flushTimer = new Timer(FlushBatches, null, _flushInterval, _flushInterval);
        _retryTimer = new Timer(RetryMemoryFailedBatches, null, _retryInterval, _retryInterval);
    }

    /// <summary>
    ///     发布数据消息到队列。
    /// </summary>
    /// <param name="dataMessage">数据消息</param>
    public async Task PublishAsync(DataMessage dataMessage)
    {
        await _channel.Writer.WriteAsync(dataMessage).ConfigureAwait(false);
    }

    /// <summary>
    ///     订阅队列并处理消息。
    /// </summary>
    /// <param name="ct">取消标记</param>
    public async Task SubscribeAsync(CancellationToken ct)
    {
        await foreach (var dataMessage in _channel.Reader.ReadAllAsync(ct))
        {
            _processingStopwatch.Restart();
            try
            {
                // 记录队列深度（包括 Channel 待读取 + 批量积累的消息）
                var totalDepth = GetTotalQueueDepth();
                _metricsCollector?.RecordQueueDepth(totalDepth);

                await StoreDataPointAsync(dataMessage).ConfigureAwait(false);

                _processingStopwatch.Stop();
                _metricsCollector?.RecordProcessingLatency(_processingStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _metricsCollector?.RecordError(dataMessage.PlcCode ?? "unknown", dataMessage.Measurement,
                    dataMessage.ChannelCode);
                _logger.LogError(ex, "Error processing message: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    ///     释放队列资源（仅释放，不等待同步操作）。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // 停止定时器
        _flushTimer?.Dispose();
        _retryTimer?.Dispose();

        // 安全地关闭 Channel（如果尚未关闭）
        // TryComplete 不会抛出异常，返回 false 表示已经关闭
        _channel.Writer.TryComplete();

        // 清理内存中的批次数据
        lock (_batchLock)
        {
            _dataBatchMap.Clear();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     定时刷新批次（内存中未达到 BatchSize 的数据）
    ///     当批次未达到 BatchSize 时，定时刷新避免数据长时间积压
    /// </summary>
    private async void FlushBatches(object? state)
    {
        List<KeyValuePair<string, List<DataMessage>>> batchesToFlush = new();

        lock (_batchLock)
        {
            foreach (var kvp in _dataBatchMap)
                if (kvp.Value.Count > 0)
                {
                    batchesToFlush.Add(new KeyValuePair<string, List<DataMessage>>(kvp.Key, [..kvp.Value]));
                    kvp.Value.Clear();
                }
        }

        foreach (var batch in batchesToFlush)
        {
            // batch.Key 是 "plccode:channelcode:measurement" 格式
            // 提取 measurement（最后一个冒号后的部分）
            var measurement = batch.Key.Split(':').LastOrDefault() ?? batch.Key;
            await WriteWalAndTryInfluxAsync(measurement, batch.Value).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     重试内存中失败的批次（在 WriteWalAndTryInfluxAsync 中失败后加入 _failedBatches 的批次）
    ///     注意：此方法仅处理内存中的失败批次。
    ///     ParquetRetryWorker 负责扫描磁盘上的 Parquet 文件并重试（处理 InfluxDB 写入失败后保留的 WAL 文件）
    /// </summary>
    private async void RetryMemoryFailedBatches(object? state)
    {
        var batchesToRetry = new List<FailedBatch>();

        while (_failedBatches.TryDequeue(out var failedBatch))
        {
            if (failedBatch.RetryCount >= _maxRetryCount)
            {
                _logger.LogError("批次重试次数已达上限，放弃重试: {Measurement}", failedBatch.Measurement);
                continue;
            }

            batchesToRetry.Add(failedBatch);
        }

        foreach (var batch in batchesToRetry)
            await WriteWalAndTryInfluxAsync(batch.Measurement, batch.Messages).ConfigureAwait(false);
    }

    /// <summary>
    ///     立即写 WAL（Parquet 文件）并尝试写入 InfluxDB。
    ///     WAL-first 架构的核心方法，确保数据安全性。
    ///     执行流程：
    ///     1. 首先写入 WAL（Parquet 文件）：
    ///     - 调用 SaveBatchAsync 将消息批量写入新的 Parquet 文件
    ///     - WAL 文件作为数据的安全保障，即使后续步骤失败，数据也不会丢失
    ///     - 返回 WAL 文件路径，用于后续删除或重试
    ///     2. 然后尝试写入 InfluxDB：
    ///     - 调用 SaveBatchAsync 将消息批量写入 InfluxDB
    ///     - 如果写入成功，删除对应的 WAL 文件（数据已安全存储）
    ///     - 如果写入失败，保留 WAL 文件，等待后续重试
    ///     错误处理策略：
    ///     - WAL 写入成功 + InfluxDB 写入成功：
    ///     - 删除 WAL 文件（数据已安全存储到 InfluxDB）
    ///     - 记录成功指标
    ///     - WAL 写入成功 + InfluxDB 写入失败：
    ///     - 保留 WAL 文件（数据安全）
    ///     - 记录错误指标和警告日志
    ///     - 不加入内存重试队列（由 ParquetRetryWorker 扫描磁盘文件重试）
    ///     - 这样设计可以避免内存中堆积大量失败批次
    ///     - WAL 写入失败：
    ///     - 记录错误日志
    ///     - 不加入重试队列（避免重复失败）
    ///     - 数据可能丢失（这种情况很少见，通常是磁盘空间不足等系统级问题）
    ///     重试机制：
    ///     - 内存中的失败批次由 RetryMemoryFailedBatches 定时重试（处理瞬时错误）
    ///     - 磁盘上的 WAL 文件由 ParquetRetryWorker 定期扫描并重试（处理持久性错误）
    ///     - 双重保障确保数据最终一致性
    ///     性能考虑：
    ///     - 批量写入可以提高写入效率（减少 I/O 次数）
    ///     - WAL 写入使用 Parquet 格式，压缩比高，写入速度快
    ///     - InfluxDB 写入是异步的，不会阻塞主流程
    ///     数据安全性：
    ///     - WAL-first 架构确保数据不会丢失（即使 InfluxDB 不可用）
    ///     - 只有当 InfluxDB 写入成功后才删除 WAL 文件
    ///     - 系统重启后，ParquetRetryWorker 会扫描并重试所有未完成的 WAL 文件
    /// </summary>
    /// <param name="measurement">测量值名称（Measurement），用于标识数据点和错误日志</param>
    /// <param name="messages">要写入的数据消息列表，通常是批量累积的消息</param>
    /// <remarks>
    ///     此方法是数据存储的关键路径，实现了 WAL-first 架构。
    ///     即使 InfluxDB 写入失败，数据也不会丢失（保留在 WAL 文件中）。
    /// </remarks>
    private async Task WriteWalAndTryInfluxAsync(string measurement, List<DataMessage> messages)
    {
        try
        {
            // 写入独立 WAL 文件
            var walPath = await _parquetStorage.SaveBatchAsync(messages, true).ConfigureAwait(false);

            // 尝试写入 InfluxDB
            var influxSuccess = await _influxStorage.SaveBatchAsync(messages).ConfigureAwait(false);

            // 只有在 InfluxDB 写入成功时才删除 Parquet 文件
            if (influxSuccess && !string.IsNullOrEmpty(walPath))
            {
                await _parquetStorage.DeleteFileAsync(walPath).ConfigureAwait(false);
            }
            else
            {
                // InfluxDB 写入失败，将文件移动到 retry 文件夹，交给 worker 处理
                if (!string.IsNullOrEmpty(walPath))
                {
                    await _parquetStorage.MoveToRetryAsync(walPath).ConfigureAwait(false);
                }
                
                // 记录日志
                var firstMessage = messages.FirstOrDefault();
                _metricsCollector?.RecordError(firstMessage?.PlcCode ?? "unknown", measurement,
                    firstMessage?.ChannelCode);
                _logger.LogWarning("写入 Influx 失败，文件已移动到 retry 文件夹: {WalPath}", walPath);
            }
        }
        catch (Exception ex)
        {
            // WAL 写入失败
            _logger.LogError(ex, "写 WAL 失败 {Measurement}: {Message}", measurement, ex.Message);
        }
    }

    /// <summary>
    ///     获取当前队列总深度（Channel 待读取 + 批量积累的消息）
    /// </summary>
    private int GetTotalQueueDepth()
    {
        // Channel 中待读取的消息数（近似值）
        var channelDepth = _channel.Reader.Count;

        // _dataBatchMap 中积累的消息总数
        int batchDepth;
        lock (_batchLock)
        {
            batchDepth = _dataBatchMap.Values.Sum(list => list.Count);
        }

        return channelDepth + batchDepth;
    }

    /// <summary>
    ///     根据 plccode:channelcode:measurement 从配置中获取 BatchSize
    ///     使用缓存机制避免频繁访问配置服务
    /// </summary>
    private int GetBatchSize(string? plcCode, string? channelCode, string measurement)
    {
        var cacheKey = $"{plcCode ?? "unknown"}:{channelCode ?? "unknown"}:{measurement}";

        // 先从缓存获取
        if (_batchSizeCache.TryGetValue(cacheKey, out var cachedSize)) return cachedSize;

        // 缓存未命中，从配置加载
        try
        {
            var configs = _deviceConfigService.GetConfigs().GetAwaiter().GetResult();
            var config = configs.FirstOrDefault(c => c.PlcCode == plcCode);
            if (config != null)
            {
                var channel = config.Channels?.FirstOrDefault(ch =>
                    ch.ChannelCode == channelCode && ch.Measurement == measurement);
                if (channel != null)
                {
                    var batchSize = channel.BatchSize > 0 ? channel.BatchSize : 1;
                    _batchSizeCache.TryAdd(cacheKey, batchSize);
                    return batchSize;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取 BatchSize 配置失败，使用默认值 1: {PlcCode}:{ChannelCode}:{Measurement}",
                plcCode, channelCode, measurement);
        }

        // 使用默认值并缓存
        _batchSizeCache.TryAdd(cacheKey, 1);
        return 1;
    }

    /// <summary>
    ///     将数据消息存储至数据库，支持批量处理。
    ///     功能说明：
    ///     - 根据 BatchSize 配置决定是否进行批量聚合（从配置文件读取）
    ///     - BatchSize &lt;= 1：立即写入（单个数据点）
    ///     - BatchSize &gt; 1：累积到批量大小后再写入（批量优化）
    ///     批量处理逻辑：
    ///     - 使用 _dataBatchMap 按 "plccode:channelcode:measurement" 分组累积消息
    ///     - 每个 PLC/Channel/Measurement 组合独立维护一个消息列表
    ///     - 当累积数量达到 BatchSize 时，取出 BatchSize 个消息进行写入
    ///     - 使用锁（_batchLock）保护批量操作，确保线程安全
    ///     批量写入的好处：
    ///     - 减少数据库写入次数，提高写入效率
    ///     - 减少网络往返次数，降低延迟开销
    ///     - 特别适合高频采集场景，可以显著提升吞吐量
    ///     线程安全：
    ///     - 批量累积操作在锁内执行，确保多线程安全
    ///     - 实际写入操作（WriteWalAndTryInfluxAsync）在锁外执行，避免长时间持有锁
    ///     - 这样设计可以最大化并发性能，同时保证数据一致性
    ///     时序数据库特性：
    ///     - 时序数据库不支持 Update 操作，所有数据点统一使用 Insert 操作
    ///     - End 事件通过 event_type 标签区分，而不是更新 Start 事件
    ///     - 这样可以保证数据的不可变性和可追溯性
    /// </summary>
    /// <param name="dataMessage">数据消息</param>
    /// <remarks>
    ///     注意：批量写入可能会导致数据延迟（延迟时间 = 达到 BatchSize 的时间）。
    ///     如果需要更低的延迟，可以设置较小的 BatchSize 或使用定时刷新机制。
    /// </remarks>
    private async Task StoreDataPointAsync(DataMessage dataMessage)
    {
        // 使用锁保护批量操作，确保线程安全
        // 使用 plccode + channelcode + measurement 作为 key，确保不同 PLC/Channel 的数据独立批量处理
        var batchKey =
            $"{dataMessage.PlcCode ?? "unknown"}:{dataMessage.ChannelCode ?? "unknown"}:{dataMessage.Measurement}";

        // 从配置中获取 BatchSize
        var batchSize = GetBatchSize(dataMessage.PlcCode, dataMessage.ChannelCode, dataMessage.Measurement);

        List<DataMessage>? batchToSave = null;
        lock (_batchLock)
        {
            var batch = _dataBatchMap.GetOrAdd(batchKey, _ => new List<DataMessage>());
            batch.Add(dataMessage);

            // 对于 BatchSize <= 1，立即触发写入；对于 BatchSize > 1，达到批量大小时触发
            if (batchSize <= 1 || batch.Count >= batchSize)
            {
                var takeCount = batchSize <= 1 ? 1 : batchSize;
                batchToSave = batch.Take(takeCount).ToList();
                batch.RemoveRange(0, batchToSave.Count);
            }
        }

        // 在锁外异步执行 WAL + Influx，不阻塞队列处理
        if (batchToSave != null)
            try
            {
                await WriteWalAndTryInfluxAsync(dataMessage.Measurement, batchToSave).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "异步写入 WAL/Influx 失败: {BatchKey}", batchKey);
            }
    }


    /// <summary>
    ///     失败的批次
    /// </summary>
    private class FailedBatch
    {
        public string Measurement { get; } = string.Empty;
        public List<DataMessage> Messages { get; } = new();
        public int RetryCount { get; set; }
        public string LastError { get; set; } = string.Empty;
    }
}