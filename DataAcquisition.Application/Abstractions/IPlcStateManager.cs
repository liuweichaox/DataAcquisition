using System.Collections.Concurrent;
using System.Threading;
using DataAcquisition.Application;

namespace DataAcquisition.Application.Abstractions;

public interface IPlcStateManager
{
    ConcurrentDictionary<string, PlcRuntime> Runtimes { get; }
    ConcurrentDictionary<string, IPlcClientService> PlcClients { get; }
    ConcurrentDictionary<string, bool> PlcConnectionHealth { get; }
    ConcurrentDictionary<string, SemaphoreSlim> PlcLocks { get; }
    void Clear();
}
