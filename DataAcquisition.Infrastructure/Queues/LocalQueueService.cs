using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.Queues;

/// <summary>
/// 消息队列实现，支持批量处理优化
/// </summary>
public class LocalQueueService : IQueueService
{
    private readonly IDataStorageService _dataStorage;
    private readonly IDataProcessingService _dataProcessingService;
    private readonly IOperationalEventsService _events;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly Channel<DataMessage> _channel = Channel.CreateUnbounded<DataMessage>();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    private readonly ConcurrentQueue<FailedBatch> _failedBatches = new();
    private readonly object _batchLock = new object();
    private readonly System.Diagnostics.Stopwatch _processingStopwatch = new();
    private Timer? _flushTimer;
    private Timer? _retryTimer;
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5); // 定时刷新间隔
    private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(10); // 重试间隔
    private readonly Dictionary<string, int> _dynamicBatchSizes = new(); // 动态批量大小
    private readonly Dictionary<string, double> _writeLatencies = new(); // 写入延迟记录
    private readonly object _dynamicBatchLock = new object();

    public LocalQueueService(
        IDataStorageService dataStorage,
        IDataProcessingService dataProcessingService,
        IOperationalEventsService events,
        IMetricsCollector? metricsCollector = null)
    {
        _dataStorage = dataStorage;
        _dataProcessingService = dataProcessingService;
        _events = events;
        _metricsCollector = metricsCollector;

        // 启动定时刷新和重试任务
        _flushTimer = new Timer(FlushBatches, null, _flushInterval, _flushInterval);
        _retryTimer = new Timer(RetryFailedBatches, null, _retryInterval, _retryInterval);
    }

    /// <summary>
    /// 定时刷新批次
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
            try
            {
                await _dataStorage.SaveBatchAsync(batch.Value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 保存失败，加入重试队列
                _failedBatches.Enqueue(new FailedBatch
                {
                    Measurement = batch.Key,
                    Messages = batch.Value,
                    RetryCount = 0,
                    LastError = ex.Message
                });
                await _events.ErrorAsync($"定时刷新批次失败 {batch.Key}: {ex.Message}", ex).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 重试失败的批次
    /// </summary>
    private async void RetryFailedBatches(object? state)
    {
        var batchesToRetry = new List<FailedBatch>();

        while (_failedBatches.TryDequeue(out var failedBatch))
        {
            if (failedBatch.RetryCount >= 3) // 最多重试3次
            {
                await _events.ErrorAsync($"批次重试次数已达上限，放弃重试: {failedBatch.Measurement}", null).ConfigureAwait(false);
                continue;
            }

            batchesToRetry.Add(failedBatch);
        }

        foreach (var batch in batchesToRetry)
        {
            try
            {
                await _dataStorage.SaveBatchAsync(batch.Messages).ConfigureAwait(false);
                // 重试成功，记录指标
                _metricsCollector?.RecordBatchWriteEfficiency(batch.Messages.Count, 0);
            }
            catch (Exception ex)
            {
                // 重试失败，重新加入队列
                batch.RetryCount++;
                batch.LastError = ex.Message;
                _failedBatches.Enqueue(batch);
            }
        }
    }

    /// <summary>
    /// 获取动态批量大小
    /// </summary>
    private int GetDynamicBatchSize(string measurement, int defaultBatchSize)
    {
        lock (_dynamicBatchLock)
        {
            if (_dynamicBatchSizes.TryGetValue(measurement, out var size))
            {
                return size;
            }

            // 根据写入延迟调整批量大小
            if (_writeLatencies.TryGetValue(measurement, out var latency))
            {
                if (latency > 1000) // 延迟超过1秒，减小批量大小
                {
                    size = Math.Max(1, defaultBatchSize / 2);
                }
                else if (latency < 100) // 延迟小于100ms，增大批量大小
                {
                    size = defaultBatchSize * 2;
                }
                else
                {
                    size = defaultBatchSize;
                }
            }
            else
            {
                size = defaultBatchSize;
            }

            _dynamicBatchSizes[measurement] = size;
            return size;
        }
    }

    /// <summary>
    /// 记录写入延迟并更新动态批量大小
    /// </summary>
    private void UpdateWriteLatency(string measurement, double latencyMs)
    {
        lock (_dynamicBatchLock)
        {
            _writeLatencies[measurement] = latencyMs;

            // 根据延迟调整批量大小
            if (_dynamicBatchSizes.TryGetValue(measurement, out var currentSize))
            {
                if (latencyMs > 1000 && currentSize > 1)
                {
                    _dynamicBatchSizes[measurement] = Math.Max(1, currentSize / 2);
                }
                else if (latencyMs < 100 && currentSize < 1000)
                {
                    _dynamicBatchSizes[measurement] = Math.Min(1000, currentSize * 2);
                }
            }
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
                // 记录队列深度
                _metricsCollector?.RecordQueueDepth(_channel.Reader.Count);

                await _dataProcessingService.ExecuteAsync(dataMessage).ConfigureAwait(false);
                await StoreDataPointAsync(dataMessage).ConfigureAwait(false);

                _processingStopwatch.Stop();
                _metricsCollector?.RecordProcessingLatency(_processingStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _metricsCollector?.RecordError(dataMessage.DeviceCode ?? "unknown", dataMessage.Measurement);
                await _events.ErrorAsync($"Error processing message: {ex.Message}", ex).ConfigureAwait(false);
            }
        }
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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _dataStorage.SaveAsync(dataMessage).ConfigureAwait(false);
                stopwatch.Stop();
                UpdateWriteLatency(dataMessage.Measurement, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // 单条写入失败，加入重试队列
                _failedBatches.Enqueue(new FailedBatch
                {
                    Measurement = dataMessage.Measurement,
                    Messages = new List<DataMessage> { dataMessage },
                    RetryCount = 0,
                    LastError = ex.Message
                });
                throw;
            }
            return;
        }

        // 获取动态批量大小
        var dynamicBatchSize = GetDynamicBatchSize(dataMessage.Measurement, dataMessage.BatchSize);
        var effectiveBatchSize = Math.Max(1, dynamicBatchSize);

        // 使用锁保护批量操作，确保线程安全
        List<DataMessage>? batchToSave = null;
        lock (_batchLock)
        {
            var batch = _dataBatchMap.GetOrAdd(dataMessage.Measurement, _ => new List<DataMessage>());
            batch.Add(dataMessage);

            if (batch.Count >= effectiveBatchSize)
            {
                batchToSave = new List<DataMessage>(batch);
                batch.Clear();
            }
        }

        // 在锁外执行数据库操作，避免长时间持有锁
        if (batchToSave != null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _dataStorage.SaveBatchAsync(batchToSave).ConfigureAwait(false);
                stopwatch.Stop();
                UpdateWriteLatency(dataMessage.Measurement, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // 批量写入失败，加入重试队列
                _failedBatches.Enqueue(new FailedBatch
                {
                    Measurement = dataMessage.Measurement,
                    Messages = batchToSave,
                    RetryCount = 0,
                    LastError = ex.Message
                });
                await _events.ErrorAsync($"批量写入失败 {dataMessage.Measurement}: {ex.Message}", ex).ConfigureAwait(false);
            }
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
                await _dataStorage.SaveBatchAsync(batch.Value).ConfigureAwait(false);
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
                await _dataStorage.SaveBatchAsync(failedBatch.Messages).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _events.ErrorAsync($"最终重试失败批次失败 {failedBatch.Measurement}: {ex.Message}", ex).ConfigureAwait(false);
            }
        }
    }
}
