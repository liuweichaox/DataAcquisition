using DataAcquisition.Gateway.Hubs;
using DataAcquisition.Infrastructure.OperationalEvents;
using Microsoft.AspNetCore.SignalR;

namespace DataAcquisition.Gateway.BackgroundServices;

/// <summary>
/// 从事件通道读取并通过 SignalR 广播运行事件。
/// </summary>
public sealed class OpsEventBroadcastWorker(OpsEventChannel channel, IHubContext<DataHub> hub) : BackgroundService
{
    /// <summary>
    /// 读取事件并广播到所有客户端。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in channel.Reader.ReadAllAsync(stoppingToken))
        {
            await hub.Clients.All.SendAsync("ReceiveOpsEvent", evt, stoppingToken);
        }
    }
}
