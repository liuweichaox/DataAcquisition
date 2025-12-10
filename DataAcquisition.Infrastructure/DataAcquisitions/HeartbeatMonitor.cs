using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Clients;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
/// 心跳监控器，周期性检测 PLC 连通性。
/// </summary>
public class HeartbeatMonitor : IHeartbeatMonitor
{
    private readonly IPlcStateManager _plcStateManager;
    private readonly IOperationalEventsService _events;

    /// <summary>
    /// 初始化心跳监控器。
    /// </summary>
    public HeartbeatMonitor(IPlcStateManager plcStateManager, IOperationalEventsService events)
    {
        _plcStateManager = plcStateManager;
        _events = events;
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
                if (!_plcStateManager.PlcClients.TryGetValue(config.Code, out var client))
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
                        await _events.WarnAsync($"{config.Code}-网络检测失败：IP {config.Host}，Ping 未响应").ConfigureAwait(false);
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
                            await _events.InfoAsync($"{config.Code}-心跳检测正常").ConfigureAwait(false);
                    }
                    else
                    {
                        _plcStateManager.PlcConnectionHealth[config.Code] = false;
                        await _events.WarnAsync($"{config.Code}-心跳检测失败", connect.Message).ConfigureAwait(false);
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
