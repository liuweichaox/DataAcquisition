using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Queues;

public interface IQueue : IAsyncDisposable
{
    Task PublishAsync(DataMessage dataMessage);

    Task SubscribeAsync(CancellationToken ct);
}