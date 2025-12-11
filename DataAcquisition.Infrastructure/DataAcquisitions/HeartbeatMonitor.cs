using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Clients;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
/// 心跳监控器，周期性检测 PLC 连通性。
/// 连接恢复由下次心跳检测自动完成，无需额外重连逻辑。
/// </summary>
public class HeartbeatMonitor : IHeartbeatMonitor
{
    private readonly IPLCStateManager _plcStateManager;
    private readonly IPLCClientLifecycleService _plcLifecycle;
    private readonly IOperationalEventsService _events;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly Dictionary<string, DateTime> _connectionStartTimes = new();

    /// <summary>
    /// 初始化心跳监控器。
    /// </summary>
    public HeartbeatMonitor(IPLCStateManager plcStateManager, IPLCClientLifecycleService plcLifecycle, IOperationalEventsService events, IMetricsCollector? metricsCollector = null)
    {
        _plcStateManager = plcStateManager;
        _plcLifecycle = plcLifecycle;
        _events = events;
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
                if (!_plcLifecycle.TryGetClient(config.Code, out var client))
                {
                    _plcStateManager.PlcConnectionHealth[config.Code] = false;
                    await _events.WarnAsync($"{config.Code}-未找到PLC客户端").ConfigureAwait(false);
                    await Task.Delay(config.HeartbeatPollingInterval, ct).ConfigureAwait(false);
                    continue;
                }

                var ping = client.IpAddressPing();
                var ok = ping == IPStatus.Success;

                if (!ok)
                {
                    if (lastOk)
                    {
                        await _events.WarnAsync($"{config.Code}-网络检测失败：IP {config.Host}，Ping 未响应").ConfigureAwait(false);
                        _metricsCollector?.RecordConnectionStatus(config.Code, false);
                        RecordConnectionEnd(config.Code);
                    }
                    _plcStateManager.PlcConnectionHealth[config.Code] = false;
                }
                else
                {
                    var connect = await WriteAsync(config.Code, config.HeartbeatMonitorRegister, writeData, ct).ConfigureAwait(false);
                    ok = connect.IsSuccess;
                    if (ok)
                    {
                        writeData ^= 1;
                        _plcStateManager.PlcConnectionHealth[config.Code] = true;

                        if (!lastOk)
                        {
                            await _events.InfoAsync($"{config.Code}-心跳检测正常").ConfigureAwait(false);
                            _metricsCollector?.RecordConnectionStatus(config.Code, true);
                            RecordConnectionStart(config.Code);
                        }
                    }
                    else
                    {
                        _plcStateManager.PlcConnectionHealth[config.Code] = false;
                        if (lastOk)
                        {
                            await _events.WarnAsync($"{config.Code}-心跳检测失败", connect.Message).ConfigureAwait(false);
                            _metricsCollector?.RecordConnectionStatus(config.Code, false);
                            RecordConnectionEnd(config.Code);
                        }
                        // 连接恢复由下次心跳检测自动完成，无需额外重连逻辑
                    }
                }

                lastOk = ok;
            }
            catch (Exception ex)
            {
                _plcStateManager.PlcConnectionHealth[config.Code] = false;
                await _events.ErrorAsync($"{config.Code}-系统异常: {ex.Message}", ex).ConfigureAwait(false);
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
    private void RecordConnectionStart(string deviceCode)
    {
        _connectionStartTimes[deviceCode] = DateTime.Now;
    }

    /// <summary>
    /// 记录连接结束并计算持续时间
    /// </summary>
    private void RecordConnectionEnd(string deviceCode)
    {
        if (_connectionStartTimes.TryGetValue(deviceCode, out var startTime))
        {
            var duration = (DateTime.Now - startTime).TotalSeconds;
            _metricsCollector?.RecordConnectionDuration(deviceCode, duration);
            _connectionStartTimes.Remove(deviceCode);
        }
    }

    /// <summary>
    /// 向 PLC 写入心跳测试值。
    /// </summary>
    private async Task<PlcWriteResult> WriteAsync(string plcCode, string address, ushort value, CancellationToken ct)
    {
        if (!_plcStateManager.PlcClients.TryGetValue(plcCode, out var client))
        {
            return new PlcWriteResult
            {
                IsSuccess = false,
                Message = $"未找到 PLC {plcCode}"
            };
        }

        if (!_plcStateManager.PlcLocks.TryGetValue(plcCode, out var locker))
        {
            return new PlcWriteResult
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