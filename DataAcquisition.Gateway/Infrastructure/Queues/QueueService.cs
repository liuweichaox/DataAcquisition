using System.Collections.Concurrent;
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
public class QueueService : QueueServiceBase
{
    private readonly IDataStorageService _dataStorageService;
    private readonly IMemoryCache _memoryCache;
    private readonly IDataProcessingService _dataProcessingService;
    private readonly IMessageService _messageService;
    private readonly BlockingCollection<DataMessage> _queue = new();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();

    public QueueService(IDataStorageService dataStorageService, IMemoryCache memoryCache, IDataProcessingService dataProcessingService, IMessageService messageService)
    {
        _dataStorageService = dataStorageService;
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
                await _dataStorageService.SaveBatchAsync(kv.Value);
            }
        }
    }

    private async Task StoreDataPointAsync(DataMessage dataMessage)
    {
        await _dataStorageService.SaveAsync(dataMessage);
    }

    public override void Dispose()
    {
        _queue.CompleteAdding();
    }
}
