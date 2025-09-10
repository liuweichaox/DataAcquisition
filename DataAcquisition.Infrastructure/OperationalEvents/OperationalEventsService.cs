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
public sealed class OperationalEventsService(ILogger<OperationalEventsService> log, IOpsEventBus bus)
    : IOperationalEventsService
{
    /// <summary>
    /// 记录信息级运行事件。
    /// </summary>
    public Task InfoAsync(string deviceCode, string message, object? data = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Information, deviceCode, message, data, ct);

    /// <summary>
    /// 记录警告级运行事件。
    /// </summary>
    public Task WarnAsync(string deviceCode, string message, object? data = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Warning, deviceCode, message, data, ct);

    /// <summary>
    /// 记录错误级运行事件。
    /// </summary>
    public Task ErrorAsync(string deviceCode, string message, Exception? ex = null, object? data = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Error, deviceCode, message, data, ct, ex);

    /// <summary>
    /// 记录心跳状态变化事件。
    /// </summary>
    public Task HeartbeatChangedAsync(string deviceCode, bool ok, string? detail = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Information, deviceCode, ok ? "Heartbeat OK" : "Heartbeat FAIL", new { ok, detail }, ct);

    /// <summary>
    /// 发布运行事件到消息总线。
    /// </summary>
    private async Task PublishAsync(LogLevel level, string deviceCode, string message, object? data, CancellationToken ct, Exception? ex = null)
    {
        if (data != null)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    log.LogError(ex, "[{Device}] {Message} {@Data}", deviceCode, message, data);
                    break;
                case LogLevel.Error:
                    log.LogWarning("[{Device}] {Message} {@Data}", deviceCode, message, data);
                    break;
                default:
                    log.LogInformation("[{Device}] {Message} {@Data}", deviceCode, message, data);
                    break;
            }
        }
        else
        {
            switch (level)
            {
                case LogLevel.Information:
                    log.LogError(ex, "[{Device}] {Message}", deviceCode, message);
                    break;
                case LogLevel.Warning:
                    log.LogWarning("[{Device}] {Message}", deviceCode, message);
                    break;
                default:
                    log.LogInformation("[{Device}] {Message}", deviceCode, message);
                    break;
            }
        }

        var evt = new OpsEvent(DateTimeOffset.Now, deviceCode, level.ToString(), message, data);
        await bus.PublishAsync(evt, ct);
    }
}
