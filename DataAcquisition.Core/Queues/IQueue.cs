
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IQueue : IAsyncDisposable
{
    Task PublishAsync(DataMessage dataMessage);

    Task SubscribeAsync(CancellationToken ct);
}