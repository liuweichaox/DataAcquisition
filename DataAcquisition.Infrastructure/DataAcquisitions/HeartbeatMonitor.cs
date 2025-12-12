using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Clients;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
/// 心跳监控器，周期性检测 PLC 连通性。
/// 连接恢复由下次心跳检测自动完成，无需额外重连逻辑。
/// </summary>
public class HeartbeatMonitor : IHeartbeatMonitor
{
    private readonly ConcurrentDictionary<string, bool> _plcConnectionHealth = new();
    private readonly IPLCClientLifecycleService _plcLifecycle;
    private readonly ILogger<HeartbeatMonitor> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly Dictionary<string, DateTime> _connectionStartTimes = new();

    /// <summary>
    /// 初始化心跳监控器。
    /// </summary>
    public HeartbeatMonitor(IPLCClientLifecycleService plcLifecycle, ILogger<HeartbeatMonitor> logger, IMetricsCollector? metricsCollector = null)
    {
        _plcLifecycle = plcLifecycle;
        _logger = logger;
        _metricsCollector = metricsCollector;
    }

    /// <summary>
    /// 监控指定设备的心跳状态。
    /// </summary>
    public async Task MonitorAsync(DeviceConfig config, CancellationToken ct = default)
    {
        await Task.Yield();
        var lastOk = false;
        ushort writeData = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_plcLifecycle.TryGetClient(config.PLCCode, out var client))
                {
                    _plcConnectionHealth[config.PLCCode] = false;
                    _logger.LogWarning("{PLCCode}-未找到PLC客户端", config.PLCCode);
                    await Task.Delay(config.HeartbeatPollingInterval, ct).ConfigureAwait(false);
                    continue;
                }

                var ping = client.IpAddressPing();
                var ok = ping == IPStatus.Success;

                if (!ok)
                {
                    if (lastOk)
                    {
                        _logger.LogWarning("{PLCCode}-网络检测失败：IP {Host}，Ping 未响应", config.PLCCode, config.Host);
                        _metricsCollector?.RecordConnectionStatus(config.PLCCode, false);
                        RecordConnectionEnd(config.PLCCode);
                    }
                    _plcConnectionHealth[config.PLCCode] = false;
                }
                else
                {
                    var connect = await WriteAsync(config.PLCCode, config.HeartbeatMonitorRegister, writeData, ct).ConfigureAwait(false);
                    ok = connect.IsSuccess;
                    if (ok)
                    {
                        writeData ^= 1;
                        _plcConnectionHealth[config.PLCCode] = true;

                        if (!lastOk)
                        {
                            _logger.LogInformation("{PLCCode}-心跳检测正常", config.PLCCode);
                            _metricsCollector?.RecordConnectionStatus(config.PLCCode, true);
                            RecordConnectionStart(config.PLCCode);
                        }
                    }
                    else
                    {
                        _plcConnectionHealth[config.PLCCode] = false;
                        if (lastOk)
                        {
                            _logger.LogWarning("{PLCCode}-心跳检测失败: {Message}", config.PLCCode, connect.Message);
                            _metricsCollector?.RecordConnectionStatus(config.PLCCode, false);
                            RecordConnectionEnd(config.PLCCode);
                        }
                        // 连接恢复由下次心跳检测自动完成，无需额外重连逻辑
                    }
                }

                lastOk = ok;
            }
            catch (Exception ex)
            {
                _plcConnectionHealth[config.PLCCode] = false;
                _logger.LogError(ex, "{PLCCode}-系统异常: {Message}", config.PLCCode, ex.Message);
            }
            finally
            {
                await Task.Delay(config.HeartbeatPollingInterval, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 记录连接开始时间
    /// </summary>
    private void RecordConnectionStart(string plcCode)
    {
        _connectionStartTimes[plcCode] = DateTime.Now;
    }

    /// <summary>
    /// 记录连接结束并计算持续时间
    /// </summary>
    private void RecordConnectionEnd(string plcCode)
    {
        if (_connectionStartTimes.TryGetValue(plcCode, out var startTime))
        {
            var duration = (DateTime.Now - startTime).TotalSeconds;
            _metricsCollector?.RecordConnectionDuration(plcCode, duration);
            _connectionStartTimes.Remove(plcCode);
        }
    }

    /// <summary>
    /// 获取 PLC 连接状态。
    /// </summary>
    public bool TryGetConnectionHealth(string plcCode, out bool isConnected)
    {
        return _plcConnectionHealth.TryGetValue(plcCode, out isConnected);
    }

    /// <summary>
    /// 向 PLC 写入心跳测试值。
    /// </summary>
    private async Task<PLCWriteResult> WriteAsync(string plcCode, string address, ushort value, CancellationToken ct)
    {
        if (!_plcLifecycle.TryGetClient(plcCode, out var client))
        {
            return new PLCWriteResult
            {
                IsSuccess = false,
                Message = $"未找到 PLC {plcCode}"
            };
        }

        if (!_plcLifecycle.TryGetLock(plcCode, out var locker))
        {
            return new PLCWriteResult
            {
                IsSuccess = false,
                Message = $"未找到 PLC {plcCode} 的锁对象"
            };
        }

        await locker.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await client.WriteUShortAsync(address, value).ConfigureAwait(false);
        }
        finally
        {
            locker.Release();
        }
    }
}