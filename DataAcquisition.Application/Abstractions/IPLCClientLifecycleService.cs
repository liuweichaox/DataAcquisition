using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// PLC 客户端生命周期管理：创建、获取、关闭、清理。
/// </summary>
public interface IPLCClientLifecycleService
{
    /// <summary>
    /// 获取或创建 PLC 客户端。
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>PLC 客户端实例</returns>
    IPLCClientService GetOrCreateClient(DeviceConfig config);

    /// <summary>
    /// 尝试获取 PLC 客户端。
    /// </summary>
    /// <param name="plcCode">PLC 编码</param>
    /// <param name="client">PLC 客户端实例（如果存在）</param>
    /// <returns>是否成功获取</returns>
    bool TryGetClient(string plcCode, out IPLCClientService client);

    /// <summary>
    /// 尝试获取 PLC 锁对象。
    /// </summary>
    /// <param name="plcCode">PLC 编码</param>
    /// <param name="locker">锁对象（如果存在）</param>
    /// <returns>是否成功获取</returns>
    bool TryGetLock(string plcCode, out System.Threading.SemaphoreSlim locker);

    /// <summary>
    /// 关闭指定设备的 PLC 客户端并清理相关资源。
    /// </summary>
    /// <param name="plcCode">PLC编码</param>
    Task CloseAsync(string plcCode);

    /// <summary>
    /// 关闭所有 PLC 客户端并清理相关资源。
    /// </summary>
    Task CloseAllAsync();
}
