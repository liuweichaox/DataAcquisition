using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Domain.OperationalEvents;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 运行事件订阅者接口。
/// 使用观察者模式，允许多个订阅者独立处理同一事件。
/// </summary>
public interface IOpsEventSubscriber
{
    /// <summary>
    /// 处理运行事件。
    /// </summary>
    /// <param name="evt">运行事件</param>
    /// <param name="ct">取消标记</param>
    Task HandleAsync(OpsEvent evt, CancellationToken ct = default);
}
