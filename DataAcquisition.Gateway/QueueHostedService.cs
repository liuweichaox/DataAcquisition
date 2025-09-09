using DataAcquisition.Core.Queues;
using Microsoft.Extensions.Hosting;

namespace DataAcquisition.Gateway;

/// <summary>
/// 后台服务，负责订阅并处理队列消息。
/// </summary>
public class QueueHostedService : BackgroundService
{
    private readonly IQueue _queue;
    public QueueHostedService(IQueue queue) => _queue = queue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await _queue.SubscribeAsync(stoppingToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _queue.DisposeAsync();            // Close the write side.
        await base.StopAsync(cancellationToken); // Ensure ExecuteAsync completes.
    }
}
