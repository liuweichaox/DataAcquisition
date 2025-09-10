using DataAcquisition.Gateway.Hubs;
using DataAcquisition.Infrastructure.OperationalEvents;
using Microsoft.AspNetCore.SignalR;

namespace DataAcquisition.Gateway.BackgroundServices;

/// <summary>
/// 从事件通道读取并通过 SignalR 广播运行事件。
/// </summary>
public sealed class OpsEventBroadcastWorker : BackgroundService
{
    private readonly OpsEventChannel _channel;
    private readonly IHubContext<DataHub> _hub;

    public OpsEventBroadcastWorker(OpsEventChannel channel, IHubContext<DataHub> hub)
    {
        _channel = channel;
        _hub = hub;
    }

    /// <summary>
    /// 读取事件并广播到所有客户端。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await _hub.Clients.All.SendAsync("ReceiveOpsEvent", evt, stoppingToken);
        }
    }
}
