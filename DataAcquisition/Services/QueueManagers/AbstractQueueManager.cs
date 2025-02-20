using System.Threading.Tasks;
using DataAcquisition.Services.Messages;

namespace DataAcquisition.Services.QueueManagers;

public abstract class AbstractQueueManager : IQueueManager
{
    protected AbstractQueueManager(IDataStorage dataStorage, DataAcquisitionConfig dataAcquisitionConfig, IMessageService messageService)
    {
        Task.Run(ProcessQueueAsync);
    }
    
    public abstract void EnqueueData(DataPoint dataPoint);
    protected abstract Task ProcessQueueAsync();
    public abstract void Complete();
}