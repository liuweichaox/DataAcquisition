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

namespace DataAcquisition.Infrastructure.Queues;

/// <summary>
/// 消息队列实现，支持批量处理优化
/// </summary>
public class LocalQueueService : IQueueService
{
    private readonly ParquetFileStorageService _parquetStorage;
    private readonly InfluxDbDataStorageService _influxStorage;
    private readonly IOperationalEventsService _events;
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
        IOperationalEventsService events,
        IConfiguration configuration,
        IMetricsCollector? metricsCollector = null)
    {
        _parquetStorage = parquetStorage;
        _influxStorage = influxStorage;
        _events = events;
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
                await _events.ErrorAsync($"批次重试次数已达上限，放弃重试: {failedBatch.Measurement}", null).ConfigureAwait(false);
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
    /// 立即写 WAL（Parquet 文件）并尝试写入 InfluxDB
    /// - 成功：删除 WAL 文件
    /// - InfluxDB 写入失败：保留 WAL 文件，由 ParquetRetryWorker 后续扫描并重试
    /// - WAL 写入失败：记录错误，不加入重试队列（避免重复失败）
    /// </summary>
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
                await _events.WarnAsync($"写入 Influx 失败，保留 WAL 文件: {walPath}, {ex.Message}").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync($"写 WAL 失败 {measurement}: {ex.Message}", ex).ConfigureAwait(false);
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
                await _events.ErrorAsync($"Error processing message: {ex.Message}", ex).ConfigureAwait(false);
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
    /// 时序数据库不支持Update操作，所有数据点统一使用Insert操作。
    /// </summary>
    /// <param name="dataMessage">数据消息</param>
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
                await _events.ErrorAsync($"刷新未完成批次失败 {batch.Key}: {ex.Message}", ex).ConfigureAwait(false);
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
                await _events.ErrorAsync($"最终重试失败批次失败 {failedBatch.Measurement}: {ex.Message}", ex).ConfigureAwait(false);
            }
        }
    }
}
