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
    public Task InfoAsync(string message, object? data = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Information, message, data, ct);

    /// <summary>
    /// 记录警告级运行事件。
    /// </summary>
    public Task WarnAsync(string message, object? data = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Warning, message, data, ct);

    /// <summary>
    /// 记录错误级运行事件。
    /// </summary>
    public Task ErrorAsync(string message, Exception? ex = null, object? data = null, CancellationToken ct = default)
        => PublishAsync(LogLevel.Error, message, data, ct, ex);

    /// <summary>
    /// 发布运行事件到消息总线。
    /// </summary>
    private async Task PublishAsync(LogLevel level, string message, object? data, CancellationToken ct, Exception? ex = null)
    {
        if (data != null)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    log.LogError(ex, "{Message} {@Data}", message, data);
                    break;
                case LogLevel.Error:
                    log.LogWarning("{Message} {@Data}", message, data);
                    break;
                default:
                    log.LogInformation("{Message} {@Data}", message, data);
                    break;
            }
        }
        else
        {
            switch (level)
            {
                case LogLevel.Information:
                    log.LogError(ex, "{Message}", message);
                    break;
                case LogLevel.Warning:
                    log.LogWarning("{Message}", message);
                    break;
                default:
                    log.LogInformation("{Message}", message);
                    break;
            }
        }

        var evt = new OpsEvent(DateTimeOffset.Now, level.ToString(), message, data);
        await bus.PublishAsync(evt, ct);
    }
}
