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
        => PublishAsync(LogLevel.Information, deviceCode, message, data, ct);

    public Task WarnAsync(string deviceCode, string message, object? data = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Warning, deviceCode, message, data, ct);

    public Task ErrorAsync(string deviceCode, string message, Exception? ex = null, object? data = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Error, deviceCode, message, data, ct, ex);

    public Task HeartbeatChangedAsync(string deviceCode, bool ok, string? detail = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Information, deviceCode, ok ? "Heartbeat OK" : "Heartbeat FAIL", new { ok, detail }, ct);

    private async Task PublishAsync(LogLevel level, string deviceCode, string message, object? data, CancellationToken ct, Exception? ex = null)
    {
        if (data != null)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    _log.LogError(ex, "[{Device}] {Message} {@Data}", deviceCode, message, data);
                    break;
                case LogLevel.Error:
                    _log.LogWarning("[{Device}] {Message} {@Data}", deviceCode, message, data);
                    break;
                default:
                    _log.LogInformation("[{Device}] {Message} {@Data}", deviceCode, message, data);
                    break;
            }
        }
        else
        {
            switch (level)
            {
                case LogLevel.Information:
                    _log.LogError(ex, "[{Device}] {Message}", deviceCode, message);
                    break;
                case LogLevel.Warning:
                    _log.LogWarning("[{Device}] {Message}", deviceCode, message);
                    break;
                default:
                    _log.LogInformation("[{Device}] {Message}", deviceCode, message);
                    break;
            }
        }

        var evt = new OpsEvent(DateTimeOffset.Now, deviceCode, level.ToString(), message, data);
        await _bus.PublishAsync(evt, ct);
    }
}
