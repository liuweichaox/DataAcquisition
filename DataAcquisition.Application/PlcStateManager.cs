using System.Collections.Concurrent;
using System.Threading;
using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Application;

public class PlcStateManager : IPlcStateManager
{
    public ConcurrentDictionary<string, PlcRuntime> Runtimes { get; } = new();
    public ConcurrentDictionary<string, IPlcClientService> PlcClients { get; } = new();
    public ConcurrentDictionary<string, bool> PlcConnectionHealth { get; } = new();
    public ConcurrentDictionary<string, SemaphoreSlim> PlcLocks { get; } = new();

    public void Clear()
    {
        PlcClients.Clear();
        PlcConnectionHealth.Clear();
        Runtimes.Clear();
        PlcLocks.Clear();
    }
}
