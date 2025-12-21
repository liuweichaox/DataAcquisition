using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
///     心跳监控器接口。
/// </summary>
public interface IHeartbeatMonitor
{
    /// <summary>
    ///     监控设备心跳状态。
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task MonitorAsync(DeviceConfig config, CancellationToken ct = default);

    /// <summary>
    ///     获取 PLC 连接状态。
    /// </summary>
    /// <param name="plcCode">PLC 编码</param>
    /// <param name="isConnected">连接状态（如果存在）</param>
    /// <returns>是否成功获取</returns>
    bool TryGetConnectionHealth(string plcCode, out bool isConnected);
}