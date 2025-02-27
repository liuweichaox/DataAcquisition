using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Models;
using DataAcquisition.Core.QueueManagers;

namespace DataAcquisition.Gateway.Services.QueueManagers;

public class QueueManagerFactory : IQueueManagerFactory
{
    public IQueueManager Create(IDataStorage dataStorage, DataAcquisitionConfig config, IMessageService messageService)
    {
        return new QueueManager(dataStorage, config, messageService);
    }
}