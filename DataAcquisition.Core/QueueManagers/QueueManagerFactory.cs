using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Messages;
using Microsoft.Extensions.Caching.Memory;

namespace DataAcquisition.Core.QueueManagers
{
    /// <summary>
    /// 队列管理器工厂实现
    /// </summary>
    public class QueueManagerFactory : IQueueManagerFactory
    {
        
        private readonly IDataStorageFactory _dataStorageFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly IDataProcessingService _dataProcessingService;
        private readonly IMessageService _messageService;

        /// <summary>
        /// 队列管理器工厂
        /// </summary>
        public QueueManagerFactory(
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

        /// <summary>
        /// 创建队列管理器
        /// </summary>
        /// <param name="deviceConfig"></param>
        /// <returns>队列管理器</returns>
        public IQueueManager Create(DeviceConfig deviceConfig)
        {
            var dataStorage = _dataStorageFactory.Create(deviceConfig);
            
            return new QueueManager(
                dataStorage,
                _memoryCache,
                _dataProcessingService,
                _messageService);
        }
    }
}