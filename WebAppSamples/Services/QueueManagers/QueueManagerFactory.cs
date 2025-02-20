using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.QueueManagers;

namespace WebAppSamples.Services.QueueManagers;

public class QueueManagerFactory : IQueueManagerFactory
{
    public IQueueManager Create(IDataStorage dataStorage, DataAcquisitionConfig config)
    {
        return new QueueManager(dataStorage, config);
    }
}