using System.Threading.Tasks;

namespace DataAcquisition.Core.Queues;

public abstract class QueueServiceBase : IQueueService
{
    protected QueueServiceBase()
    {
        Task.Run(ProcessQueueAsync);
    }

    public abstract void EnqueueData(DataMessage dataMessage);
    protected abstract Task ProcessQueueAsync();
    public abstract void Dispose();
}
