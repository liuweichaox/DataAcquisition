using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using HslCommunication.BasicFramework;

namespace DataAcquisition.Infrastructure.Queues;

/// <summary>
/// 消息队列实现
/// </summary>
public class LocalQueueService : IQueueService
{
    private readonly Channel<DataMessage> _channel = Channel.CreateUnbounded<DataMessage>();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    private readonly IDataStorageService _dataStorage;
    private readonly IDataProcessingService _dataProcessingService;
    private readonly IOperationalEventsService _events;

    public LocalQueueService(IDataStorageService dataStorage, IDataProcessingService dataProcessingService, IOperationalEventsService events)
    {
        _dataStorage = dataStorage;
        _dataProcessingService = dataProcessingService;
        _events = events;
    }

    public async Task PublishAsync(DataMessage dataMessage)
    {
        await _channel.Writer.WriteAsync(dataMessage);
    }

    public async Task SubscribeAsync(CancellationToken ct)
    {
        await foreach (var dataMessage in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await _dataProcessingService.ExecuteAsync(dataMessage);
                await StoreDataPointAsync(dataMessage);
            }
            catch (Exception ex)
            {
                await _events.ErrorAsync("System", $"Error processing message: {ex.Message}");
            }
        }
    }

    private async Task StoreDataPointAsync(DataMessage dataMessage)
    {
        if (dataMessage.Operation == DataOperation.Update)
        {
            await _dataStorage.UpdateAsync(
                dataMessage.TableName,
                dataMessage.DataValues.ToDictionary(k => k.Key, k => (object)k.Value),
                dataMessage.KeyValues.ToDictionary(k => k.Key, k => (object)k.Value));
            return;
        }

        if (dataMessage.BatchSize <= 1)
        {
            await _dataStorage.SaveAsync(dataMessage);
            return;
        }

        var batch = _dataBatchMap.GetOrAdd(dataMessage.TableName, _ => new List<DataMessage>());
        batch.Add(dataMessage);

        if (batch.Count >= dataMessage.BatchSize)
        {
            await _dataStorage.SaveBatchAsync(batch);
            batch.Clear();
        }
    }

    public ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        return ValueTask.CompletedTask;
    }
}
