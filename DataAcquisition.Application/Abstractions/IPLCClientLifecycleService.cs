using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// PLC 客户端生命周期管理：创建、获取、关闭、清理。
/// </summary>
public interface IPLCClientLifecycleService
{
    /// <summary>
    /// 获取或创建指定设备的 PLC 客户端，并确保锁已就绪。
    /// </summary>
    IPlcClientService GetOrCreateClient(DeviceConfig config);

    /// <summary>
    /// 尝试获取已存在的 PLC 客户端。
    /// </summary>
    /// <param name="deviceCode">PLC编码（PLCCode）</param>
    bool TryGetClient(string deviceCode, out IPlcClientService client);

    /// <summary>
    /// 尝试获取设备级锁。
    /// </summary>
    /// <param name="deviceCode">PLC编码（PLCCode）</param>
    bool TryGetLock(string deviceCode, out SemaphoreSlim locker);

    /// <summary>
    /// 关闭并移除指定设备的客户端与锁。
    /// </summary>
    /// <param name="deviceCode">PLC编码（PLCCode）</param>
    Task CloseAsync(string deviceCode);

    /// <summary>
    /// 关闭并移除所有客户端与锁。
    /// </summary>
    Task CloseAllAsync();
}
