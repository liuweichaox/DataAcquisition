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
    private readonly IMessage _message;

    public QueueFactory(
        IDataStorageFactory dataStorageFactory,
        IMemoryCache memoryCache,
        IDataProcessingService dataProcessingService,
        IMessage message)
    {
        _dataStorageFactory = dataStorageFactory;
        _memoryCache = memoryCache;
        _dataProcessingService = dataProcessingService;
        _message = message;
    }

    public IQueue Create(DeviceConfig deviceConfig)
    {
        var dataStorage = _dataStorageFactory.Create(deviceConfig);

        return new Queue(
            dataStorage,
            _memoryCache,
            _dataProcessingService,
            _message);
    }
}
