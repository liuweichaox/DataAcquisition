using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 心跳监控器接口。
/// </summary>
public interface IHeartbeatMonitor
{
    /// <summary>
    /// 监控设备心跳状态。
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task MonitorAsync(DeviceConfig config, CancellationToken ct = default);
}
