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
/// 消息队列实现
/// </summary>
public class LocalQueueService(
    IDataStorageService dataStorage,
    IDataProcessingService dataProcessingService,
    IOperationalEventsService events)
    : IQueueService
{
    private readonly Channel<DataMessage> _channel = Channel.CreateUnbounded<DataMessage>();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    private readonly object _batchLock = new object();

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
            try
            {
                await dataProcessingService.ExecuteAsync(dataMessage).ConfigureAwait(false);
                await StoreDataPointAsync(dataMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await events.ErrorAsync($"Error processing message: {ex.Message}", ex).ConfigureAwait(false);
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
            await dataStorage.SaveAsync(dataMessage).ConfigureAwait(false);
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
                batchToSave = new List<DataMessage>(batch);
                batch.Clear();
            }
        }

        // 在锁外执行数据库操作，避免长时间持有锁
        if (batchToSave != null)
        {
            await dataStorage.SaveBatchAsync(batchToSave).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 释放队列资源，并刷新所有未完成的批次。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();

        // 刷新所有未完成的批次，避免数据丢失
        lock (_batchLock)
        {
            foreach (var kvp in _dataBatchMap)
            {
                if (kvp.Value.Count > 0)
                {
                    var batch = new List<DataMessage>(kvp.Value);
                    // 使用 Task.Run 避免在 ValueTask 中使用 async/await
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await dataStorage.SaveBatchAsync(batch).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            await events.ErrorAsync($"刷新未完成批次失败: {ex.Message}", ex).ConfigureAwait(false);
                        }
                    });
                }
            }
            _dataBatchMap.Clear();
        }

        return ValueTask.CompletedTask;
    }
}
