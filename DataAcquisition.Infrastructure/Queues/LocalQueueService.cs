using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using DataAcquisition.Infrastructure.DataStorages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.Queues;

/// <summary>
/// 消息队列实现，支持批量处理优化
/// </summary>
public class LocalQueueService : IQueueService
{
    private readonly ParquetFileStorageService _parquetStorage;
    private readonly InfluxDbDataStorageService _influxStorage;
    private readonly ILogger<LocalQueueService> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly Channel<DataMessage> _channel = Channel.CreateUnbounded<DataMessage>();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    private readonly ConcurrentQueue<FailedBatch> _failedBatches = new();
    private readonly object _batchLock = new object();
    private readonly System.Diagnostics.Stopwatch _processingStopwatch = new();
    private Timer? _flushTimer;
    private Timer? _retryTimer;
    private readonly TimeSpan _flushInterval;
    private readonly TimeSpan _retryInterval;
    private readonly int _maxRetryCount;

    public LocalQueueService(
        ParquetFileStorageService parquetStorage,
        InfluxDbDataStorageService influxStorage,
        ILogger<LocalQueueService> logger,
        IConfiguration configuration,
        IMetricsCollector? metricsCollector = null)
    {
        _parquetStorage = parquetStorage;
        _influxStorage = influxStorage;
        _logger = logger;
        _metricsCollector = metricsCollector;

        var options = new Domain.Models.QueueServiceOptions
        {
            FlushIntervalSeconds = int.TryParse(configuration["Acquisition:QueueService:FlushIntervalSeconds"], out var flushInterval) ? flushInterval : 5,
            RetryIntervalSeconds = int.TryParse(configuration["Acquisition:QueueService:RetryIntervalSeconds"], out var retryInterval) ? retryInterval : 10,
            MaxRetryCount = int.TryParse(configuration["Acquisition:QueueService:MaxRetryCount"], out var maxRetry) ? maxRetry : 3
        };
        _flushInterval = TimeSpan.FromSeconds(options.FlushIntervalSeconds);
        _retryInterval = TimeSpan.FromSeconds(options.RetryIntervalSeconds);
        _maxRetryCount = options.MaxRetryCount;

        // 启动定时刷新和重试任务
        _flushTimer = new Timer(FlushBatches, null, _flushInterval, _flushInterval);
        _retryTimer = new Timer(RetryMemoryFailedBatches, null, _retryInterval, _retryInterval);
    }

    /// <summary>
    /// 定时刷新批次（内存中未达到 BatchSize 的数据）
    /// 当批次未达到 BatchSize 时，定时刷新避免数据长时间积压
    /// </summary>
    private async void FlushBatches(object? state)
    {
        List<KeyValuePair<string, List<DataMessage>>> batchesToFlush = new();

        lock (_batchLock)
        {
            foreach (var kvp in _dataBatchMap)
            {
                if (kvp.Value.Count > 0)
                {
                    batchesToFlush.Add(new KeyValuePair<string, List<DataMessage>>(kvp.Key, new List<DataMessage>(kvp.Value)));
                    kvp.Value.Clear();
                }
            }
        }

        foreach (var batch in batchesToFlush)
        {
            await WriteWalAndTryInfluxAsync(batch.Key, batch.Value).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 重试内存中失败的批次（在 WriteWalAndTryInfluxAsync 中失败后加入 _failedBatches 的批次）
    /// 注意：此方法仅处理内存中的失败批次。
    /// ParquetRetryWorker 负责扫描磁盘上的 Parquet 文件并重试（处理 InfluxDB 写入失败后保留的 WAL 文件）
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
        {
            await WriteWalAndTryInfluxAsync(batch.Measurement, batch.Messages).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// 失败的批次
    /// </summary>
    private class FailedBatch
    {
        public string Measurement { get; set; } = string.Empty;
        public List<DataMessage> Messages { get; set; } = new();
        public int RetryCount { get; set; }
        public string LastError { get; set; } = string.Empty;
    }

    /// <summary>
    /// 立即写 WAL（Parquet 文件）并尝试写入 InfluxDB。
    ///
    /// WAL-first 架构的核心方法，确保数据安全性。
    ///
    /// 执行流程：
    /// 1. 首先写入 WAL（Parquet 文件）：
    ///    - 调用 SaveBatchAsNewFileAsync 将消息批量写入新的 Parquet 文件
    ///    - WAL 文件作为数据的安全保障，即使后续步骤失败，数据也不会丢失
    ///    - 返回 WAL 文件路径，用于后续删除或重试
    ///
    /// 2. 然后尝试写入 InfluxDB：
    ///    - 调用 SaveBatchAsync 将消息批量写入 InfluxDB
    ///    - 如果写入成功，删除对应的 WAL 文件（数据已安全存储）
    ///    - 如果写入失败，保留 WAL 文件，等待后续重试
    ///
    /// 错误处理策略：
    /// - WAL 写入成功 + InfluxDB 写入成功：
    ///   - 删除 WAL 文件（数据已安全存储到 InfluxDB）
    ///   - 记录成功指标
    ///
    /// - WAL 写入成功 + InfluxDB 写入失败：
    ///   - 保留 WAL 文件（数据安全）
    ///   - 记录错误指标和警告日志
    ///   - 不加入内存重试队列（由 ParquetRetryWorker 扫描磁盘文件重试）
    ///   - 这样设计可以避免内存中堆积大量失败批次
    ///
    /// - WAL 写入失败：
    ///   - 记录错误日志
    ///   - 不加入重试队列（避免重复失败）
    ///   - 数据可能丢失（这种情况很少见，通常是磁盘空间不足等系统级问题）
    ///
    /// 重试机制：
    /// - 内存中的失败批次由 RetryMemoryFailedBatches 定时重试（处理瞬时错误）
    /// - 磁盘上的 WAL 文件由 ParquetRetryWorker 定期扫描并重试（处理持久性错误）
    /// - 双重保障确保数据最终一致性
    ///
    /// 性能考虑：
    /// - 批量写入可以提高写入效率（减少 I/O 次数）
    /// - WAL 写入使用 Parquet 格式，压缩比高，写入速度快
    /// - InfluxDB 写入是异步的，不会阻塞主流程
    ///
    /// 数据安全性：
    /// - WAL-first 架构确保数据不会丢失（即使 InfluxDB 不可用）
    /// - 只有当 InfluxDB 写入成功后才删除 WAL 文件
    /// - 系统重启后，ParquetRetryWorker 会扫描并重试所有未完成的 WAL 文件
    /// </summary>
    /// <param name="measurement">测量值名称（Measurement），用于标识数据点和错误日志</param>
    /// <param name="messages">要写入的数据消息列表，通常是批量累积的消息</param>
    /// <remarks>
    /// 此方法是数据存储的关键路径，实现了 WAL-first 架构。
    /// 即使 InfluxDB 写入失败，数据也不会丢失（保留在 WAL 文件中）。
    /// </remarks>
    private async Task WriteWalAndTryInfluxAsync(string measurement, List<DataMessage> messages)
    {
        try
        {
            // 写入独立 WAL 文件
            var walPath = await _parquetStorage.SaveBatchAsNewFileAsync(messages).ConfigureAwait(false);

            try
            {
                await _influxStorage.SaveBatchAsync(messages).ConfigureAwait(false);
                await _parquetStorage.DeleteFileAsync(walPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Influx 失败，保留 WAL 文件，记录日志
                var firstMessage = messages.FirstOrDefault();
                _metricsCollector?.RecordError(firstMessage?.PLCCode ?? "unknown", measurement, firstMessage?.ChannelCode);
                _logger.LogWarning(ex, "写入 Influx 失败，保留 WAL 文件: {WalPath}, {Message}", walPath, ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写 WAL 失败 {Measurement}: {Message}", measurement, ex.Message);
        }
    }

    /// <summary>
    /// 发布数据消息到队列。
    /// </summary>
    /// <param name="dataMessage">数据消息</param>
    public async Task PublishAsync(DataMessage dataMessage)
    {
        await _channel.Writer.WriteAsync(dataMessage).ConfigureAwait(false);
    }

    /// <summary>
    /// 订阅队列并处理消息。
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
                _metricsCollector?.RecordError(dataMessage.PLCCode ?? "unknown", dataMessage.Measurement, dataMessage.ChannelCode);
                _logger.LogError(ex, "Error processing message: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 获取当前队列总深度（Channel 待读取 + 批量积累的消息）
    /// </summary>
    private int GetTotalQueueDepth()
    {
        // Channel 中待读取的消息数（近似值）
        var channelDepth = _channel.Reader.Count;

        // _dataBatchMap 中积累的消息总数
        int batchDepth = 0;
        lock (_batchLock)
        {
            batchDepth = _dataBatchMap.Values.Sum(list => list.Count);
            }

        return channelDepth + batchDepth;
    }

    /// <summary>
    /// 将数据消息存储至数据库，支持批量处理。
    ///
    /// 功能说明：
    /// - 根据 BatchSize 配置决定是否进行批量聚合
    /// - BatchSize &lt;= 1：立即写入（单个数据点）
    /// - BatchSize &gt; 1：累积到批量大小后再写入（批量优化）
    ///
    /// 批量处理逻辑：
    /// - 使用 _dataBatchMap 按 Measurement 分组累积消息
    /// - 每个 Measurement 独立维护一个消息列表
    /// - 当累积数量达到 BatchSize 时，取出 BatchSize 个消息进行写入
    /// - 使用锁（_batchLock）保护批量操作，确保线程安全
    ///
    /// 批量写入的好处：
    /// - 减少数据库写入次数，提高写入效率
    /// - 减少网络往返次数，降低延迟开销
    /// - 特别适合高频采集场景，可以显著提升吞吐量
    ///
    /// 线程安全：
    /// - 批量累积操作在锁内执行，确保多线程安全
    /// - 实际写入操作（WriteWalAndTryInfluxAsync）在锁外执行，避免长时间持有锁
    /// - 这样设计可以最大化并发性能，同时保证数据一致性
    ///
    /// 时序数据库特性：
    /// - 时序数据库不支持 Update 操作，所有数据点统一使用 Insert 操作
    /// - End 事件通过 event_type 标签区分，而不是更新 Start 事件
    /// - 这样可以保证数据的不可变性和可追溯性
    /// </summary>
    /// <param name="dataMessage">数据消息，包含 Measurement、BatchSize 等信息</param>
    /// <remarks>
    /// 注意：批量写入可能会导致数据延迟（延迟时间 = 达到 BatchSize 的时间）。
    /// 如果需要更低的延迟，可以设置较小的 BatchSize 或使用定时刷新机制。
    /// </remarks>
    private async Task StoreDataPointAsync(DataMessage dataMessage)
    {
        // 时序数据库统一使用Insert操作，End事件通过event_type标签区分
        if (dataMessage.BatchSize <= 1)
        {
            await WriteWalAndTryInfluxAsync(dataMessage.Measurement, new List<DataMessage> { dataMessage }).ConfigureAwait(false);
            return;
        }

        // 使用锁保护批量操作，确保线程安全
        List<DataMessage>? batchToSave = null;
        lock (_batchLock)
        {
            var batch = _dataBatchMap.GetOrAdd(dataMessage.Measurement, _ => new List<DataMessage>());
            batch.Add(dataMessage);

            if (batch.Count >= dataMessage.BatchSize)
            {
                batchToSave = batch.Take(dataMessage.BatchSize).ToList();
                batch.RemoveRange(0, dataMessage.BatchSize);
            }
        }

        // 在锁外执行 WAL + Influx
        if (batchToSave != null)
        {
            await WriteWalAndTryInfluxAsync(dataMessage.Measurement, batchToSave).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 释放队列资源，并刷新所有未完成的批次。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // 停止定时器
        _flushTimer?.Dispose();
        _retryTimer?.Dispose();

        // 安全地关闭 Channel（如果尚未关闭）
        // TryComplete 不会抛出异常，返回 false 表示已经关闭
        _channel.Writer.TryComplete();

        // 刷新所有未完成的批次，避免数据丢失
        List<KeyValuePair<string, List<DataMessage>>> batchesToFlush = new();
        lock (_batchLock)
        {
            foreach (var kvp in _dataBatchMap)
            {
                if (kvp.Value.Count > 0)
                {
                    batchesToFlush.Add(new KeyValuePair<string, List<DataMessage>>(kvp.Key, new List<DataMessage>(kvp.Value)));
                }
            }
            _dataBatchMap.Clear();
        }

        // 同步刷新所有批次
        foreach (var batch in batchesToFlush)
        {
            try
            {
                await WriteWalAndTryInfluxAsync(batch.Key, batch.Value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新未完成批次失败 {BatchKey}: {Message}", batch.Key, ex.Message);
            }
        }

        // 尝试最后一次重试失败的批次
        while (_failedBatches.TryDequeue(out var failedBatch))
        {
            try
            {
                await WriteWalAndTryInfluxAsync(failedBatch.Measurement, failedBatch.Messages).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "最终重试失败批次失败 {Measurement}: {Message}", failedBatch.Measurement, ex.Message);
            }
        }
    }
}
