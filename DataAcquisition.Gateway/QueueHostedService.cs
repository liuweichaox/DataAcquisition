public class QueueHostedService : BackgroundService
{
    private readonly IQueue _queue;
    public QueueHostedService(IQueue queue) => _queue = queue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        =>await _queue.SubscribeAsync(stoppingToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _queue.DisposeAsync();            // 完成写端
        await base.StopAsync(cancellationToken); // 等待 ExecuteAsync 正常结束
    }
}