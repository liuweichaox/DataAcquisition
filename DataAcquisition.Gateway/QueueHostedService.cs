public class QueueHostedService : BackgroundService
{
    private readonly IQueue _queue;
    public QueueHostedService(IQueue queue) => _queue = queue;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _queue.SubscribeAsync(stoppingToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _queue.DisposeAsync();            // 完成写端
        await base.StopAsync(cancellationToken); // 等待 ExecuteAsync 正常结束
    }
}