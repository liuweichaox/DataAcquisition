using System.Threading.Tasks;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Delegates;

namespace DataAcquisition.Core.QueueManagers;

public abstract class AbstractQueueManager : IQueueManager
{
    protected AbstractQueueManager(IDataStorage dataStorage, DataAcquisitionConfig dataAcquisitionConfig,
        MessageSendDelegate messageSendDelegate)
    {
        Task.Run(ProcessQueueAsync);
    }

    public abstract void EnqueueData(DataPoint dataPoint);
    protected abstract Task ProcessQueueAsync();
    public abstract void Complete();
}