using System;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.OperationalEvents;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.OperationalEvents;

/// <summary>
/// 运行事件记录服务。
/// </summary>
public sealed class OperationalEventsService : IOperationalEventsService
{
    private readonly ILogger<OperationalEventsService> _log;
    private readonly IOpsEventBus _bus;

    public OperationalEventsService(ILogger<OperationalEventsService> log, IOpsEventBus bus)
    {
        _log = log;
        _bus = bus;
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
                _log.LogError(ex, "[{Device}] {Message} {@Data}", deviceCode, message, data);
                break;
            case "Warn":
                _log.LogWarning("[{Device}] {Message} {@Data}", deviceCode, message, data);
                break;
            default:
                _log.LogInformation("[{Device}] {Message} {@Data}", deviceCode, message, data);
                break;
        }

        var evt = new OpsEvent(DateTimeOffset.Now, deviceCode, level, message, data);
        await _bus.PublishAsync(evt, ct);
    }
}
