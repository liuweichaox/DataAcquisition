using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

public interface IQueueService : IAsyncDisposable
{
    Task PublishAsync(DataMessage dataMessage);

    Task SubscribeAsync(CancellationToken ct);
}