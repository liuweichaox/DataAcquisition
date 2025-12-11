using System;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
/// PLC 客户端生命周期管理：创建、获取、关闭、清理。
/// </summary>
public class PLCClientLifecycleService : IPLCClientLifecycleService
{
    private readonly IPLCStateManager _plcStateManager;
    private readonly IPlcClientFactory _plcClientFactory;
    private readonly IOperationalEventsService _events;

    public PLCClientLifecycleService(
        IPLCStateManager plcStateManager,
        IPlcClientFactory plcClientFactory,
        IOperationalEventsService events)
    {
        _plcStateManager = plcStateManager;
        _plcClientFactory = plcClientFactory;
        _events = events;
    }

    public IPlcClientService GetOrCreateClient(DeviceConfig config)
    {
        // 双重检查，确保多线程安全
        if (_plcStateManager.PlcClients.TryGetValue(config.Code, out var existing))
        {
            return existing;
        }

        lock (_plcStateManager.PlcClients)
        {
            if (_plcStateManager.PlcClients.TryGetValue(config.Code, out existing))
            {
                return existing;
            }

            var client = _plcClientFactory.Create(config);
            _plcStateManager.PlcClients.TryAdd(config.Code, client);
            _plcStateManager.PlcLocks.TryAdd(config.Code, new SemaphoreSlim(1, 1));
            return client;
        }
    }

    public bool TryGetClient(string deviceCode, out IPlcClientService client)
    {
        return _plcStateManager.PlcClients.TryGetValue(deviceCode, out client!);
    }

    public bool TryGetLock(string deviceCode, out SemaphoreSlim locker)
    {
        return _plcStateManager.PlcLocks.TryGetValue(deviceCode, out locker!);
    }

    public async Task CloseAsync(string deviceCode)
    {
        if (_plcStateManager.PlcClients.TryRemove(deviceCode, out var client))
        {
            try
            {
                await client.ConnectCloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _events.ErrorAsync($"关闭PLC客户端失败 {deviceCode}: {ex.Message}", ex).ConfigureAwait(false);
            }
        }

        if (_plcStateManager.PlcLocks.TryRemove(deviceCode, out var sem))
        {
            try
            {
                sem.Dispose();
            }
            catch (Exception ex)
            {
                await _events.ErrorAsync($"释放信号量失败 {deviceCode}: {ex.Message}", ex).ConfigureAwait(false);
            }
        }

        _plcStateManager.PlcConnectionHealth.TryRemove(deviceCode, out _);
    }

    public async Task CloseAllAsync()
    {
        foreach (var kv in _plcStateManager.PlcClients.Keys)
        {
            await CloseAsync(kv).ConfigureAwait(false);
        }
    }
}
