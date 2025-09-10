using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Gateway.BackgroundServices;

/// <summary>
/// 后台服务，负责订阅并处理队列消息。
/// </summary>
public class QueueHostedService : BackgroundService
{
    private readonly IQueueService _queue;
    public QueueHostedService(IQueueService queue) => _queue = queue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await _queue.SubscribeAsync(stoppingToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _queue.DisposeAsync();            // Close the write side.
        await base.StopAsync(cancellationToken); // Ensure ExecuteAsync completes.
    }
}
