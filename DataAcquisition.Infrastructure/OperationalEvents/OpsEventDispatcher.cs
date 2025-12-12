using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.OperationalEvents;

/// <summary>
/// 运行事件分发器。
/// 从事件通道读取事件并分发给所有订阅者，支持并行处理和错误隔离。
/// </summary>
public sealed class OpsEventDispatcher : BackgroundService
{
    private readonly OpsEventChannel _channel;
    private readonly IReadOnlyList<IOpsEventSubscriber> _subscribers;
    private readonly ILogger<OpsEventDispatcher> _logger;

    public OpsEventDispatcher(
        OpsEventChannel channel,
        IEnumerable<IOpsEventSubscriber> subscribers,
        ILogger<OpsEventDispatcher> logger)
    {
        _channel = channel;
        _subscribers = subscribers.ToList().AsReadOnly();
        _logger = logger;

        if (_subscribers.Count == 0)
        {
            _logger.LogWarning("未注册任何事件订阅者，事件将不会被处理");
        }
        else
        {
            _logger.LogInformation("事件分发器已初始化，共 {Count} 个订阅者", _subscribers.Count);
        }
    }

    /// <summary>
    /// 从事件通道读取事件并并行分发给所有订阅者。
    /// 采用错误隔离策略：单个订阅者的失败不会影响其他订阅者。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("事件分发器已启动");

        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await DispatchToSubscribersAsync(evt, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("事件分发器正在停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "事件分发器发生未预期的异常");
            throw;
        }
        finally
        {
            _logger.LogInformation("事件分发器已停止");
        }
    }

    /// <summary>
    /// 将事件分发给所有订阅者，并行执行，错误隔离。
    /// </summary>
    private async Task DispatchToSubscribersAsync(
        Domain.OperationalEvents.OpsEvent evt,
        CancellationToken ct)
    {
        if (_subscribers.Count == 0)
        {
            return;
        }

        // 并行通知所有订阅者，提高吞吐量
        var tasks = _subscribers.Select(subscriber =>
            HandleWithErrorIsolationAsync(subscriber, evt, ct));

        // 等待所有订阅者完成（即使部分失败也继续）
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // 统计成功和失败数量
        var successCount = results.Count(r => r);
        var failureCount = results.Count(r => !r);

        if (failureCount > 0)
        {
            _logger.LogWarning(
                "事件 [{Level}] {Message} 处理完成：成功 {SuccessCount}/{TotalCount}，失败 {FailureCount}/{TotalCount}",
                evt.Level, evt.Message, successCount, _subscribers.Count, failureCount, _subscribers.Count);
        }
    }

    /// <summary>
    /// 处理订阅者事件，捕获异常并记录日志，实现错误隔离。
    /// </summary>
    private async Task<bool> HandleWithErrorIsolationAsync(
        IOpsEventSubscriber subscriber,
        Domain.OperationalEvents.OpsEvent evt,
        CancellationToken ct)
    {
        try
        {
            await subscriber.HandleAsync(evt, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            // 取消操作是预期的，不记录为错误
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "订阅者 {SubscriberType} 处理事件失败: [{Level}] {Message}",
                subscriber.GetType().Name, evt.Level, evt.Message);
            return false;
        }
    }
}
