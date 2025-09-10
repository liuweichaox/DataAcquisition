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
                var client = _plcStateManager.PlcClients[config.Code];
                var ping = client.IpAddressPing();
                var ok = ping == IPStatus.Success;

                if (!ok)
                {
                    await _events.WarnAsync(config.Code, $"网络检测失败：IP {config.Host}，Ping 未响应");
                    _plcStateManager.PlcConnectionHealth[config.Code] = false;
                }
                else
                {
                    var connect = await WriteAsync(config.Code, config.HeartbeatMonitorRegister, writeData, ct);
                    ok = connect.IsSuccess;
                    if (ok)
                    {
                        writeData ^= 1;
                        _plcStateManager.PlcConnectionHealth[config.Code] = true;
                        if (!lastOk)
                            await _events.HeartbeatChangedAsync(config.Code, true);
                    }
                    else
                    {
                        _plcStateManager.PlcConnectionHealth[config.Code] = false;
                        await _events.HeartbeatChangedAsync(config.Code, false, connect.Message);
                    }
                }

                lastOk = ok;
            }
            catch (Exception ex)
            {
                _plcStateManager.PlcConnectionHealth[config.Code] = false;
                await _events.ErrorAsync(config.Code, $"系统异常: {ex.Message}", ex);
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

        var locker = _plcStateManager.PlcLocks[plcCode];
        await locker.WaitAsync(ct);
        try
        {
            return await client.WriteUShortAsync(address, value);
        }
        finally
        {
            locker.Release();
        }
    }
}
