using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
/// PLC 客户端生命周期管理：创建、获取、关闭、清理。
/// </summary>
public class PLCClientLifecycleService : IPLCClientLifecycleService
{
    private readonly ConcurrentDictionary<string, IPlcClientService> _plcClients = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _plcLocks = new();
    private readonly IPLCClientFactory _plcClientFactory;
    private readonly ILogger<PLCClientLifecycleService> _logger;

    public PLCClientLifecycleService(
        IPLCClientFactory plcClientFactory,
        ILogger<PLCClientLifecycleService> logger)
    {
        _plcClientFactory = plcClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 获取或创建 PLC 客户端（线程安全）。
    /// </summary>
    public IPlcClientService GetOrCreateClient(DeviceConfig config)
    {
        // 先尝试获取已存在的客户端（快速路径）
        if (_plcClients.TryGetValue(config.PLCCode, out var existingClient))
        {
            return existingClient;
        }

        // 使用 GetOrAdd 确保线程安全创建
        // 如果多个线程同时调用，只有一个会执行工厂方法创建客户端
        var client = _plcClients.GetOrAdd(config.PLCCode, _ =>
        {
            var newClient = _plcClientFactory.Create(config);
            // 同时创建对应的锁对象
            _plcLocks.TryAdd(config.PLCCode, new SemaphoreSlim(1, 1));
            return newClient;
        });

        return client;
    }

    /// <summary>
    /// 尝试获取 PLC 客户端。
    /// </summary>
    public bool TryGetClient(string plcCode, out IPlcClientService client)
    {
        return _plcClients.TryGetValue(plcCode, out client!);
    }

    /// <summary>
    /// 尝试获取 PLC 锁对象。
    /// </summary>
    public bool TryGetLock(string plcCode, out SemaphoreSlim locker)
    {
        return _plcLocks.TryGetValue(plcCode, out locker!);
    }

    /// <summary>
    /// 关闭指定设备的 PLC 客户端并清理相关资源。
    /// </summary>
    public async Task CloseAsync(string plcCode)
    {
        if (_plcClients.TryRemove(plcCode, out var client))
        {
            try
            {
                await client.ConnectCloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "关闭 PLC 客户端失败 {PLCCode}: {Message}", plcCode, ex.Message);
            }
        }

        if (_plcLocks.TryRemove(plcCode, out var locker))
        {
            try
            {
                locker.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放 PLC 锁失败 {PLCCode}: {Message}", plcCode, ex.Message);
            }
        }
    }

    /// <summary>
    /// 关闭所有 PLC 客户端并清理相关资源。
    /// </summary>
    public async Task CloseAllAsync()
    {
        var tasks = new List<Task>();
        var plcCodes = _plcClients.Keys.ToList();

        foreach (var plcCode in plcCodes)
        {
            tasks.Add(CloseAsync(plcCode));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
