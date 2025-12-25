using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Worker.BackgroundServices;

/// <summary>
///     后台服务，负责订阅并处理队列消息。
/// </summary>
public class QueueHostedService(IQueueService queue) : BackgroundService
{
    /// <summary>
    ///     执行后台订阅任务。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await queue.SubscribeAsync(stoppingToken);
    }

    /// <summary>
    ///     停止后台服务并释放队列。
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await queue.DisposeAsync(); // Close the write side.
        await base.StopAsync(cancellationToken); // Ensure ExecuteAsync completes.
    }
}