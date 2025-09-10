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
    /// <param name="deviceCode">设备编码</param>
    /// <param name="message">事件消息</param>
    /// <param name="data">附加数据</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task InfoAsync(string deviceCode, string message, object? data = null, CancellationToken ct = default);

    /// <summary>
    /// 记录警告级别事件。
    /// </summary>
    /// <param name="deviceCode">设备编码</param>
    /// <param name="message">事件消息</param>
    /// <param name="data">附加数据</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task WarnAsync(string deviceCode, string message, object? data = null, CancellationToken ct = default);

    /// <summary>
    /// 记录错误级别事件。
    /// </summary>
    /// <param name="deviceCode">设备编码</param>
    /// <param name="message">事件消息</param>
    /// <param name="ex">异常对象</param>
    /// <param name="data">附加数据</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task ErrorAsync(string deviceCode, string message, Exception? ex = null, object? data = null, CancellationToken ct = default);

    /// <summary>
    /// 心跳状态变化回调。
    /// </summary>
    /// <param name="deviceCode">设备编码</param>
    /// <param name="ok">心跳是否正常</param>
    /// <param name="detail">异常详情</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task HeartbeatChangedAsync(string deviceCode, bool ok, string? detail = null, CancellationToken ct = default);
}
