using System.Threading.Tasks;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Messages;

namespace DataAcquisition.Core.QueueManagers;

public abstract class AbstractQueueManager : IQueueManager
{
    protected AbstractQueueManager(IDataStorage dataStorage, DataAcquisitionConfig dataAcquisitionConfig,
        IMessageService messageService)
    {
        Task.Run(ProcessQueueAsync);
    }

    public abstract void EnqueueData(DataPoint dataPoint);
    protected abstract Task ProcessQueueAsync();
    public abstract void Complete();
}