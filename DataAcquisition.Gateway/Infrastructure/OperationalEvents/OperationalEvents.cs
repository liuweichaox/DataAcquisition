using System;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Core.OperationalEvents;
using DataAcquisition.Gateway.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Gateway.Infrastructure.OperationalEvents;

public sealed class OperationalEvents : IOperationalEvents
{
    private readonly ILogger<OperationalEvents> _log;
    private readonly IHubContext<DataHub> _hub;

    public OperationalEvents(ILogger<OperationalEvents> log, IHubContext<DataHub> hub)
    {
        _log = log;
        _hub = hub;
    }

    public Task InfoAsync(string deviceCode, string message, object? data = null, CancellationToken ct = default)
        => PublishAsync("Info", deviceCode, message, data, ct);

    public Task WarnAsync(string deviceCode, string message, object? data = null, CancellationToken ct = default)
        => PublishAsync("Warn", deviceCode, message, data, ct);

    public Task ErrorAsync(string deviceCode, string message, Exception? ex = null, object? data = null, CancellationToken ct = default)
        => PublishAsync("Error", deviceCode, message, data, ct, ex);

    public Task HeartbeatChangedAsync(string deviceCode, bool ok, string? detail = null, CancellationToken ct = default)
        => PublishAsync("Heartbeat", deviceCode, ok ? "Heartbeat OK" : "Heartbeat FAIL", new { ok, detail }, ct);

    private async Task PublishAsync(string level, string deviceCode, string message, object? data, CancellationToken ct, Exception? ex = null)
    {
        switch (level)
        {
            case "Error":
                _log.LogError(ex, "{Level} [{Device}] {Message} {@Data}", level, deviceCode, message, data);
                break;
            case "Warn":
                _log.LogWarning("{Level} [{Device}] {Message} {@Data}", level, deviceCode, message, data);
                break;
            default:
                _log.LogInformation("{Level} [{Device}] {Message} {@Data}", level, deviceCode, message, data);
                break;
        }

        var evt = new OpsEvent(DateTimeOffset.Now, deviceCode, level, message, data);
        await _hub.Clients.All.SendAsync("ReceiveOpsEvent", evt, ct);
    }
}
