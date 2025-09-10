using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Gateway.Hubs;
using DataAcquisition.Infrastructure.OperationalEvents;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace DataAcquisition.Gateway.OperationalEvents;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await _hub.Clients.All.SendAsync("ReceiveOpsEvent", evt, stoppingToken);
        }
    }
}
