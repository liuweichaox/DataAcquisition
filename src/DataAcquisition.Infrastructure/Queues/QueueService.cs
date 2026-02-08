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
    private readonly ConcurrentDictionary<string, int> _batchSizeCache = new();
    private readonly Channel<DataMessage> _channel = Channel.CreateUnbounded<DataMessage>();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    private readonly IDeviceConfigService _deviceConfigService;
    private readonly TimeSpan _flushInterval;
    private readonly Timer? _flushTimer;
    private readonly IDataStorageService _primaryStorage;
    private readonly IWalStorageService _walStorage;
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
        _walStorage = walStorage;
        _primaryStorage = primaryStorage;
        _logger = logger;
        _metricsCollector = metricsCollector;
        _deviceConfigService = deviceConfigService;

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

        // 刷新积压的批次数据，避免丢失
        List<KeyValuePair<string, List<DataMessage>>> remaining;
        lock (_batchLock)
        {
            remaining = _dataBatchMap
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp => new KeyValuePair<string, List<DataMessage>>(kvp.Key, [.. kvp.Value]))
                .ToList();
            _dataBatchMap.Clear();
        }

        foreach (var batch in remaining)
        {
            var measurement = batch.Key.Split(':').LastOrDefault() ?? batch.Key;
            await PersistBatchAsync(measurement, batch.Value).ConfigureAwait(false);
        }
    }

    /// <summary>定时刷新未达 BatchSize 的积压批次。</summary>
    private async void FlushBatches(object? state)
    {
        try
        {
            List<KeyValuePair<string, List<DataMessage>>> batchesToFlush;
            lock (_batchLock)
            {
                batchesToFlush = _dataBatchMap
                    .Where(kvp => kvp.Value.Count > 0)
                    .Select(kvp => new KeyValuePair<string, List<DataMessage>>(kvp.Key, [.. kvp.Value]))
                    .ToList();
                foreach (var kvp in batchesToFlush)
                    _dataBatchMap[kvp.Key].Clear();
            }

            foreach (var batch in batchesToFlush)
            {
                var measurement = batch.Key.Split(':').LastOrDefault() ?? batch.Key;
                await PersistBatchAsync(measurement, batch.Value).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定时刷新批次失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    ///     WAL-first 持久化：先写 WAL 保证数据安全，再写主存储。
    ///     主存储成功则删除 WAL；失败则将 WAL 移入重试队列。
    /// </summary>
    private async Task PersistBatchAsync(string measurement, List<DataMessage> messages)
    {
        try
        {
            var walPath = await _walStorage.WriteAsync(messages).ConfigureAwait(false);
            var success = await _primaryStorage.SaveBatchAsync(messages).ConfigureAwait(false);

            if (success && !string.IsNullOrEmpty(walPath))
            {
                await _walStorage.DeleteAsync(walPath).ConfigureAwait(false);
            }
            else if (!string.IsNullOrEmpty(walPath))
            {
                await _walStorage.MoveToRetryAsync(walPath).ConfigureAwait(false);
                var first = messages.FirstOrDefault();
                _metricsCollector?.RecordError(first?.PlcCode ?? "unknown", measurement, first?.ChannelCode);
                _logger.LogWarning("主存储写入失败，WAL 已移入重试队列: {WalPath}", walPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WAL 持久化失败 {Measurement}: {Message}", measurement, ex.Message);
        }
    }

    private int GetTotalQueueDepth()
    {
        int batchDepth;
        lock (_batchLock) { batchDepth = _dataBatchMap.Values.Sum(list => list.Count); }
        return _channel.Reader.Count + batchDepth;
    }

    private int GetBatchSize(string? plcCode, string? channelCode, string measurement)
    {
        var cacheKey = $"{plcCode ?? "unknown"}:{channelCode ?? "unknown"}:{measurement}";

        return _batchSizeCache.GetOrAdd(cacheKey, _ =>
        {
            try
            {
                var configs = _deviceConfigService.GetConfigs().GetAwaiter().GetResult();
                var channel = configs
                    .FirstOrDefault(c => c.PlcCode == plcCode)
                    ?.Channels?.FirstOrDefault(ch => ch.ChannelCode == channelCode && ch.Measurement == measurement);
                return channel?.BatchSize > 0 ? channel.BatchSize : 1;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取 BatchSize 配置失败，使用默认值 1: {CacheKey}", cacheKey);
                return 1;
            }
        });
    }

    /// <summary>
    ///     按 plcCode:channelCode:measurement 批量聚合，达到 BatchSize 后 WAL-first 持久化。
    ///     BatchSize ≤ 1 时立即写入。
    /// </summary>
    private async Task StoreDataPointAsync(DataMessage msg)
    {
        var batchKey = $"{msg.PlcCode ?? "unknown"}:{msg.ChannelCode ?? "unknown"}:{msg.Measurement}";
        var batchSize = GetBatchSize(msg.PlcCode, msg.ChannelCode, msg.Measurement);

        List<DataMessage>? batchToSave = null;
        lock (_batchLock)
        {
            var batch = _dataBatchMap.GetOrAdd(batchKey, _ => new List<DataMessage>());
            batch.Add(msg);
            if (batchSize <= 1 || batch.Count >= batchSize)
            {
                batchToSave = [.. batch];
                batch.Clear();
            }
        }

        if (batchToSave != null)
            await PersistBatchAsync(msg.Measurement, batchToSave).ConfigureAwait(false);
    }
}