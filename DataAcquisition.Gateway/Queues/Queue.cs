using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DataAcquisition.Gateway.Queues;

/// <summary>
/// 消息队列实现
/// </summary>
public class Queue : QueueBase
{
    private readonly IDataStorage _dataStorage;
    private readonly IMemoryCache _memoryCache;
    private readonly IDataProcessingService _dataProcessingService;
    private readonly IMessage _message;
    private readonly BlockingCollection<DataMessage> _queue = new();
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
        _queue.Add(dataMessage);
    }

    protected override async Task ProcessQueueAsync()
    {
        foreach (var dataMessage in _queue.GetConsumingEnumerable())
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
        await _dataStorage.SaveAsync(dataMessage);
    }

    public override void Dispose()
    {
        _queue.CompleteAdding();
    }
}
