using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Utils;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace DataAcquisition.Core.QueueManagers;

/// <summary>
/// 消息队列实现
/// </summary>
public class QueueManager: AbstractQueueManager
{
    private readonly IDataStorage _dataStorage;
    private readonly IMemoryCache _memoryCache;
    private readonly IDataProcessingService _dataProcessingService;
    private readonly IMessageService _messageService;
    private readonly BlockingCollection<DataMessage> _queue = new();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    public QueueManager(IDataStorage dataStorage, IMemoryCache memoryCache, IDataProcessingService dataProcessingService, IMessageService messageService)
    {
        _dataStorage = dataStorage;
        _memoryCache = memoryCache;
        _dataProcessingService = dataProcessingService;
        _messageService = messageService;
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
                await _messageService.SendAsync($"{ex.Message} - StackTrace: {ex.StackTrace}");
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