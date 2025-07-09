using System.Threading.Tasks;

namespace DataAcquisition.Core.QueueManagers;

public abstract class AbstractQueueManager : IQueueManager
{
    protected AbstractQueueManager()
    {
        Task.Run(ProcessQueueAsync);
    }

    public abstract void EnqueueData(DataMessage dataMessage);
    protected abstract Task ProcessQueueAsync();
    public abstract void Dispose();
}