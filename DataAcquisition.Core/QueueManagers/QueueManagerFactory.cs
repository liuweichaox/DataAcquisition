using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Delegates;

namespace DataAcquisition.Core.QueueManagers;

public class QueueManagerFactory : IQueueManagerFactory
{
    public IQueueManager Create(IDataStorage dataStorage, DataAcquisitionConfig config, MessageSendDelegate messageSendDelegate)
    {
        return new QueueManager(dataStorage, config, messageSendDelegate);
    }
}