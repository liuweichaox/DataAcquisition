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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataAcquisition.Infrastructure.Queues;

/// <summary>
///     消息队列实现：批量聚合 → WAL 持久化 → 主存储写入，失败时降级到重试队列。
/// </summary>
public class QueueService : IQueueService
{
    private readonly object _batchLock = new();
    private readonly Channel<DataMessage> _channel = Channel.CreateUnbounded<DataMessage>();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    private readonly BatchSizeResolver _batchSizeResolver;
    private readonly QueueBatchPersister _batchPersister;
    private readonly TimeSpan _flushInterval;
    private readonly Timer? _flushTimer;
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly ILogger<QueueService> _logger;
    private readonly IMetricsCollector? _metricsCollector;

    public QueueService(
        IWalStorageService walStorage,
        IDataStorageService primaryStorage,
        ILogger<QueueService> logger,
        IOptions<AcquisitionOptions> acquisitionOptions,
        IDeviceConfigService deviceConfigService,
        IMetricsCollector? metricsCollector = null)
    {
        _logger = logger;
        _metricsCollector = metricsCollector;
        _batchSizeResolver = new BatchSizeResolver(deviceConfigService, logger);
        _batchPersister = new QueueBatchPersister(walStorage, primaryStorage, logger, metricsCollector);

        var options = acquisitionOptions.Value.QueueService;
        _flushInterval = TimeSpan.FromSeconds(options.FlushIntervalSeconds);
        _flushTimer = new Timer(FlushBatches, null, _flushInterval, _flushInterval);
    }

    public async Task PublishAsync(DataMessage dataMessage) =>
        await _channel.Writer.WriteAsync(dataMessage).ConfigureAwait(false);

    public async Task SubscribeAsync(CancellationToken ct)
    {
        await foreach (var msg in _channel.Reader.ReadAllAsync(ct))
        {
            var sw = Stopwatch.StartNew();
            try
            {
                _metricsCollector?.RecordQueueDepth(GetTotalQueueDepth());
                await StoreDataPointAsync(msg).ConfigureAwait(false);
                sw.Stop();
                _metricsCollector?.RecordProcessingLatency(sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _metricsCollector?.RecordError(msg.PlcCode ?? "unknown", msg.Measurement, msg.ChannelCode);
                _logger.LogError(ex, "处理消息失败: {Message}", ex.Message);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _flushTimer?.Dispose();
        _channel.Writer.TryComplete();

        await _flushSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var remaining = TakePendingBatches(clearAll: true);
            foreach (var batch in remaining)
            {
                var persisted = await _batchPersister.PersistAsync(batch.Measurement, batch.Messages).ConfigureAwait(false);
                if (!persisted)
                    _logger.LogCritical(
                        "服务释放期间 WAL 持久化失败，内存批次未能落盘: {BatchKey}, Count={Count}",
                        batch.BatchKey,
                        batch.Messages.Count);
            }
        }
        finally
        {
            _flushSemaphore.Release();
            _flushSemaphore.Dispose();
        }
    }

    /// <summary>定时刷新未达 BatchSize 的积压批次。</summary>
    private void FlushBatches(object? state)
    {
        _ = FlushPendingBatchesAsync();
    }

    private int GetTotalQueueDepth()
    {
        int batchDepth;
        lock (_batchLock) { batchDepth = _dataBatchMap.Values.Sum(list => list.Count); }
        return _channel.Reader.Count + batchDepth;
    }

    private int GetBatchSize(string? plcCode, string? channelCode, string measurement)
        => _batchSizeResolver.GetBatchSize(plcCode, channelCode, measurement);

    /// <summary>
    ///     按 plcCode:channelCode:measurement 批量聚合，达到 BatchSize 后 WAL-first 持久化。
    ///     BatchSize ≤ 1 时立即写入。
    /// </summary>
    private async Task StoreDataPointAsync(DataMessage msg)
    {
        var batchKey = $"{msg.PlcCode ?? "unknown"}:{msg.ChannelCode ?? "unknown"}:{msg.Measurement}";
        var batchSize = GetBatchSize(msg.PlcCode, msg.ChannelCode, msg.Measurement);

        PendingBatch? batchToSave = null;
        lock (_batchLock)
        {
            var batch = _dataBatchMap.GetOrAdd(batchKey, _ => new List<DataMessage>());
            batch.Add(msg);
            if (batchSize <= 1 || batch.Count >= batchSize)
            {
                batchToSave = new PendingBatch(batchKey, msg.Measurement, [.. batch]);
                _dataBatchMap.TryRemove(batchKey, out _);
            }
        }

        if (batchToSave != null)
        {
            var persisted = await _batchPersister.PersistAsync(batchToSave.Measurement, batchToSave.Messages)
                .ConfigureAwait(false);
            if (!persisted)
                RequeueBatch(batchToSave.BatchKey, batchToSave.Messages);
        }
    }

    private List<PendingBatch> TakePendingBatches(bool clearAll)
    {
        lock (_batchLock)
        {
            var batches = _dataBatchMap
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp => new PendingBatch(kvp.Key, GetMeasurementFromBatchKey(kvp.Key), [.. kvp.Value]))
                .ToList();

            if (clearAll)
            {
                foreach (var batch in batches)
                    _dataBatchMap.TryRemove(batch.BatchKey, out _);
            }

            return batches;
        }
    }

    private void RequeueBatch(string batchKey, List<DataMessage> messages)
    {
        if (messages.Count == 0)
            return;

        lock (_batchLock)
        {
            var batch = _dataBatchMap.GetOrAdd(batchKey, _ => new List<DataMessage>());
            batch.InsertRange(0, messages);
        }

        _logger.LogWarning("WAL 写入失败，批次已回补到内存队列: {BatchKey}, Count={Count}", batchKey, messages.Count);
    }

    private static string GetMeasurementFromBatchKey(string batchKey) =>
        batchKey.Split(':').LastOrDefault() ?? batchKey;

    private async Task FlushPendingBatchesAsync()
    {
        if (!await _flushSemaphore.WaitAsync(0).ConfigureAwait(false))
        {
            _logger.LogDebug("上一轮定时刷新仍在执行，跳过本轮 Flush。");
            return;
        }

        try
        {
            var batchesToFlush = TakePendingBatches(clearAll: true);
            foreach (var batch in batchesToFlush)
            {
                var persisted = await _batchPersister.PersistAsync(batch.Measurement, batch.Messages)
                    .ConfigureAwait(false);
                if (!persisted)
                    RequeueBatch(batch.BatchKey, batch.Messages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定时刷新批次失败: {Message}", ex.Message);
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }
}
