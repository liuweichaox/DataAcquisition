using System.Collections.Concurrent;
using System.Threading;
using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Application;

/// <summary>
/// PLC 状态管理器，维护 PLC 的运行时信息与连接状态。
/// </summary>
public class PlcStateManager : IPlcStateManager
{
    public ConcurrentDictionary<string, PlcRuntime> Runtimes { get; } = new();
    public ConcurrentDictionary<string, IPlcClientService> PlcClients { get; } = new();
    public ConcurrentDictionary<string, bool> PlcConnectionHealth { get; } = new();
    public ConcurrentDictionary<string, SemaphoreSlim> PlcLocks { get; } = new();

    /// <summary>
    /// 清空所有维护的状态。
    /// </summary>
    public void Clear()
    {
        PlcClients.Clear();
        PlcConnectionHealth.Clear();
        Runtimes.Clear();
        PlcLocks.Clear();
    }
}
