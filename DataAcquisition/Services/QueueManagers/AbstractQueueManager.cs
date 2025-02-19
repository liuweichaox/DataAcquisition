using System.Threading.Tasks;
using DataAcquisition.Common;

namespace DataAcquisition.Services.QueueManagers;

public abstract class AbstractQueueManager : IQueueManager
{
    public AbstractQueueManager(DataStorageFactory dataStorageFactory, DataAcquisitionConfig dataAcquisitionConfig)
    {
        Task.Run(ProcessQueueAsync);
    }
    
    public abstract void EnqueueData(DataPoint dataPoint);
    protected abstract Task ProcessQueueAsync();
    public abstract void Complete();
}