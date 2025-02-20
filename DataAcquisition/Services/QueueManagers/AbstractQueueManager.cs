using System.Threading.Tasks;

namespace DataAcquisition.Services.QueueManagers;

public abstract class AbstractQueueManager : IQueueManager
{
    public AbstractQueueManager(IDataStorage dataStorage, DataAcquisitionConfig dataAcquisitionConfig)
    {
        Task.Run(ProcessQueueAsync);
    }
    
    public abstract void EnqueueData(DataPoint dataPoint);
    protected abstract Task ProcessQueueAsync();
    public abstract void Complete();
}