using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.Messages;
using DataAcquisition.Services.QueueManagers;

namespace DataAcquisitionGateway.Services.QueueManagers;

public class QueueManagerFactory : IQueueManagerFactory
{
    public IQueueManager Create(IDataStorage dataStorage, DataAcquisitionConfig config, IMessageService messageService)
    {
        return new QueueManager(dataStorage, config, messageService);
    }
}