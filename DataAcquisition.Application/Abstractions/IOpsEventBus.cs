using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Domain.OperationalEvents;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 运行事件消息总线接口。
/// </summary>
public interface IOpsEventBus
{
    /// <summary>
    /// 发布运行事件。
    /// </summary>
    /// <param name="evt">运行事件</param>
    /// <param name="ct">取消标记</param>
    ValueTask PublishAsync(OpsEvent evt, CancellationToken ct = default);
}
