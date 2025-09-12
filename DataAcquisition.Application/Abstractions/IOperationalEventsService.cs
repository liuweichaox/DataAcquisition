using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 运行事件记录服务接口。
/// </summary>
public interface IOperationalEventsService
{
    /// <summary>
    /// 记录信息级别事件。
    /// </summary>
    /// <param name="message">事件消息</param>
    /// <param name="data">附加数据</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task InfoAsync(string message, object? data = null, CancellationToken ct = default);

    /// <summary>
    /// 记录警告级别事件。
    /// </summary>
    /// <param name="message">事件消息</param>
    /// <param name="data">附加数据</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task WarnAsync(string message, object? data = null, CancellationToken ct = default);

    /// <summary>
    /// 记录错误级别事件。
    /// </summary>
    /// <param name="message">事件消息</param>
    /// <param name="ex">异常对象</param>
    /// <param name="data">附加数据</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task ErrorAsync(string message, Exception? ex = null, object? data = null, CancellationToken ct = default);
}
