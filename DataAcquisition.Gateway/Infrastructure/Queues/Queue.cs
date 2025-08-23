using System.Collections.Concurrent;
using System.Threading.Channels;
using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Models;
using DataAcquisition.Core.Queues;
using Microsoft.Extensions.Caching.Memory;

namespace DataAcquisition.Gateway.Infrastructure.Queues;

/// <summary>
/// 消息队列实现
/// </summary>
public class Queue : QueueBase
{
    private readonly IDataStorage _dataStorage;
    private readonly IMemoryCache _memoryCache;
    private readonly IDataProcessingService _dataProcessingService;
    private readonly IMessage _message;
    private readonly Channel<DataMessage> _channel = Channel.CreateUnbounded<DataMessage>();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();

    public Queue(IDataStorage dataStorage, IMemoryCache memoryCache, IDataProcessingService dataProcessingService, IMessage message)
    {
        _dataStorage = dataStorage;
        _memoryCache = memoryCache;
        _dataProcessingService = dataProcessingService;
        _message = message;
    }

    public override void EnqueueData(DataMessage dataMessage)
    {
        _channel.Writer.TryWrite(dataMessage);
    }

    protected override async Task ProcessQueueAsync()
    {
        await foreach (var dataMessage in _channel.Reader.ReadAllAsync())
        {
            try
            {
                await _dataProcessingService.ExecuteAsync(dataMessage);
                await StoreDataPointAsync(dataMessage);
            }
            catch (Exception ex)
            {
                await _message.SendAsync($"{ex.Message} - StackTrace: {ex.StackTrace}");
            }
        }

        foreach (var kv in _dataBatchMap)
        {
            if (kv.Value.Any())
            {
                await _dataStorage.SaveBatchAsync(kv.Value);
            }
        }
    }

    private async Task StoreDataPointAsync(DataMessage dataMessage)
    {
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

    public override void Dispose()
    {
        _channel.Writer.Complete();
    }
}
