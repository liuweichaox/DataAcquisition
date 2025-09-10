using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Gateway.BackgroundServices;

/// <summary>
/// 后台服务，负责订阅并处理队列消息。
/// </summary>
public class QueueHostedService : BackgroundService
{
    private readonly IQueueService _queue;
    public QueueHostedService(IQueueService queue) => _queue = queue;

    /// <summary>
    /// 执行后台订阅任务。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await _queue.SubscribeAsync(stoppingToken);

    /// <summary>
    /// 停止后台服务并释放队列。
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _queue.DisposeAsync();            // Close the write side.
        await base.StopAsync(cancellationToken); // Ensure ExecuteAsync completes.
    }
}
