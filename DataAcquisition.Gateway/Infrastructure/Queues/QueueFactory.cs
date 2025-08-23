using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Models;
using DataAcquisition.Core.Queues;
using Microsoft.Extensions.Caching.Memory;

namespace DataAcquisition.Gateway.Infrastructure.Queues;

/// <summary>
/// 队列工厂实现
/// </summary>
public class QueueFactory : IQueueFactory
{
    private readonly IDataStorageFactory _dataStorageFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly IDataProcessingService _dataProcessingService;
    private readonly IMessageService _messageService;

    public QueueFactory(
        IDataStorageFactory dataStorageFactory,
        IMemoryCache memoryCache,
        IDataProcessingService dataProcessingService,
        IMessageService messageService)
    {
        _dataStorageFactory = dataStorageFactory;
        _memoryCache = memoryCache;
        _dataProcessingService = dataProcessingService;
        _messageService = messageService;
    }

    public IQueueService Create(DeviceConfig deviceConfig)
    {
        var dataStorage = _dataStorageFactory.Create(deviceConfig);

        return new QueueService(
            dataStorage,
            _memoryCache,
            _dataProcessingService,
            _messageService);
    }
}
