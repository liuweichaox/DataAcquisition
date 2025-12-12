using System;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.OperationalEvents;
using DataAcquisition.Gateway.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.OperationalEvents;

/// <summary>
/// SignalR 事件订阅者。
/// 负责将运行事件推送到前端页面，支持错误处理和日志记录。
/// </summary>
public sealed class SignalREventSubscriber : IOpsEventSubscriber
{
    private readonly IHubContext<DataHub> _hub;
    private readonly ILogger<SignalREventSubscriber> _logger;

    public SignalREventSubscriber(
        IHubContext<DataHub> hub,
        ILogger<SignalREventSubscriber> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    /// <summary>
    /// 处理运行事件并推送到 SignalR。
    /// 失败时会记录日志，但不抛出异常（错误隔离）。
    /// </summary>
    public async Task HandleAsync(OpsEvent evt, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.All.SendAsync("ReceiveOpsEvent", evt, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 操作取消是预期的，忽略（Fire-and-Forget 策略）
        }
        catch (Exception ex)
        {
            // SignalR 推送失败时记录错误日志
            // 但不抛出异常，避免影响其他订阅者（错误隔离策略）
            _logger.LogWarning(ex,
                "SignalR 推送失败: [{Level}] {Message}",
                evt.Level, evt.Message);
        }
    }
}
