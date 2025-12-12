using System;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.OperationalEvents;

namespace DataAcquisition.Infrastructure.OperationalEvents;

/// <summary>
/// 运行事件发布服务。
/// 负责发布运行事件到消息总线，不关心谁订阅了这些事件。
/// 日志记录和 SignalR 推送由各自的订阅者处理。
/// </summary>
public sealed class OperationalEventsService : IOperationalEventsService
{
    private readonly IOpsEventBus _bus;

    public OperationalEventsService(IOpsEventBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    /// <summary>
    /// 发布信息级运行事件。
    /// </summary>
    /// <param name="message">事件消息</param>
    /// <param name="data">附加数据</param>
    /// <param name="ct">取消标记</param>
    /// <exception cref="ArgumentException">当消息为空时抛出</exception>
    public Task InfoAsync(string message, object? data = null, CancellationToken ct = default)
    {
        ValidateMessage(message);
        var evt = CreateEvent("Information", message, data);
        return _bus.PublishAsync(evt, ct).AsTask();
    }

    /// <summary>
    /// 发布警告级运行事件。
    /// </summary>
    /// <param name="message">事件消息</param>
    /// <param name="data">附加数据</param>
    /// <param name="ct">取消标记</param>
    /// <exception cref="ArgumentException">当消息为空时抛出</exception>
    public Task WarnAsync(string message, object? data = null, CancellationToken ct = default)
    {
        ValidateMessage(message);
        var evt = CreateEvent("Warning", message, data);
        return _bus.PublishAsync(evt, ct).AsTask();
    }

    /// <summary>
    /// 发布错误级运行事件。
    /// </summary>
    /// <param name="message">事件消息</param>
    /// <param name="ex">异常对象</param>
    /// <param name="data">附加数据</param>
    /// <param name="ct">取消标记</param>
    /// <exception cref="ArgumentException">当消息为空时抛出</exception>
    public Task ErrorAsync(string message, Exception? ex = null, object? data = null, CancellationToken ct = default)
    {
        ValidateMessage(message);

        // 将异常信息合并到 data 中，方便订阅者处理
        var eventData = BuildErrorEventData(ex, data);
        var evt = CreateEvent("Error", message, eventData);

        return _bus.PublishAsync(evt, ct).AsTask();
    }

    /// <summary>
    /// 验证消息参数。
    /// </summary>
    private static void ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("事件消息不能为空", nameof(message));
        }
    }

    /// <summary>
    /// 创建运行事件对象。
    /// </summary>
    private static OpsEvent CreateEvent(string level, string message, object? data)
    {
        return new OpsEvent(DateTimeOffset.Now, level, message, data);
    }

    /// <summary>
    /// 构建错误事件数据，合并异常信息和原始数据。
    /// </summary>
    private static object? BuildErrorEventData(Exception? ex, object? data)
    {
        if (ex == null)
        {
            return data;
        }

        // 构建包含异常信息的结构化数据
        return new
        {
            Exception = new
            {
                Type = ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace
            },
            OriginalData = data
        };
    }
}
