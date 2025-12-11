using System.Collections.Concurrent;
using System.Threading;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// PLC 状态管理器接口。
/// </summary>
public interface IPLCStateManager
{
    /// <summary>
    /// 运行时状态集合。
    /// </summary>
    ConcurrentDictionary<string, PlcRuntime> Runtimes { get; }

    /// <summary>
    /// PLC 通讯客户端集合。
    /// </summary>
    ConcurrentDictionary<string, IPlcClientService> PlcClients { get; }

    /// <summary>
    /// PLC 连接健康状态集合。
    /// </summary>
    ConcurrentDictionary<string, bool> PlcConnectionHealth { get; }

    /// <summary>
    /// PLC 锁集合。
    /// </summary>
    ConcurrentDictionary<string, SemaphoreSlim> PlcLocks { get; }

    /// <summary>
    /// 清除所有状态。
    /// </summary>
    void Clear();
}
