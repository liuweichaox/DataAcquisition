using System.Threading.Tasks;

namespace DataAcquisition.Core.Queues;

public abstract class QueueBase : IQueue
{
    protected QueueBase()
    {
        Task.Run(ProcessQueueAsync);
    }

    public abstract void EnqueueData(DataMessage dataMessage);
    protected abstract Task ProcessQueueAsync();
    public abstract void Dispose();
}
