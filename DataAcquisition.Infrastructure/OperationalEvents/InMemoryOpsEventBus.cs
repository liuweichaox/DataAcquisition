using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.OperationalEvents;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.OperationalEvents;

/// <summary>
/// 内存事件总线实现。
/// 直接调用所有订阅者，无需中间缓冲层，更简洁高效。
/// </summary>
public sealed class InMemoryOpsEventBus : IOpsEventBus
{
    private readonly IReadOnlyList<IOpsEventSubscriber> _subscribers;
    private readonly ILogger<InMemoryOpsEventBus>? _logger;

    public InMemoryOpsEventBus(
        IEnumerable<IOpsEventSubscriber> subscribers,
        ILogger<InMemoryOpsEventBus>? logger = null)
    {
        _subscribers = subscribers.ToList().AsReadOnly();
        _logger = logger;

        if (_subscribers.Count == 0)
        {
            _logger?.LogWarning("未注册任何事件订阅者，事件将不会被处理");
        }
        else
        {
            _logger?.LogInformation("事件总线已初始化，共 {Count} 个订阅者", _subscribers.Count);
        }
    }

    /// <summary>
    /// 发布事件到所有订阅者，使用 Fire-and-Forget 策略并行处理。
    /// 不阻塞发布者，订阅者异常不影响其他订阅者。
    /// </summary>
    public ValueTask PublishAsync(OpsEvent evt, CancellationToken ct = default)
    {
        if (_subscribers.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        // Fire-and-Forget：不等待完成，避免阻塞发布者
        // 在后台线程池中并行执行所有订阅者
        _ = Task.Run(async () =>
        {
            var tasks = _subscribers.Select(subscriber =>
                HandleWithErrorIsolationAsync(subscriber, evt, ct));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }, ct);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 处理订阅者事件，捕获异常实现错误隔离。
    /// </summary>
    private async Task HandleWithErrorIsolationAsync(
        IOpsEventSubscriber subscriber,
        OpsEvent evt,
        CancellationToken ct)
    {
        try
        {
            await subscriber.HandleAsync(evt, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 取消操作是预期的，忽略
        }
        catch (Exception ex)
        {
            // 单个订阅者异常不影响其他订阅者（错误隔离）
            // 记录错误日志，但由订阅者自己负责处理异常和业务日志
            _logger?.LogError(ex,
                "订阅者 {SubscriberType} 处理事件失败: [{Level}] {Message}",
                subscriber.GetType().Name, evt.Level, evt.Message);
        }
    }
}
