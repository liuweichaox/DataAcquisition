using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
///     心跳监控器，周期性检测 PLC 连通性。
///     连接恢复由下次心跳检测自动完成，无需额外重连逻辑。
/// </summary>
public class HeartbeatMonitor : IHeartbeatMonitor
{
    private readonly Dictionary<string, DateTime> _connectionStartTimes = new();
    private readonly ILogger<HeartbeatMonitor> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly ConcurrentDictionary<string, bool> _plcConnectionHealth = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastConnectedTimes = new();
    private readonly ConcurrentDictionary<string, string?> _lastErrors = new();
    private readonly IPlcClientLifecycleService _plcLifecycle;

    /// <summary>
    ///     初始化心跳监控器。
    /// </summary>
    public HeartbeatMonitor(IPlcClientLifecycleService plcLifecycle, ILogger<HeartbeatMonitor> logger,
        IMetricsCollector? metricsCollector = null)
    {
        _plcLifecycle = plcLifecycle;
        _logger = logger;
        _metricsCollector = metricsCollector;
    }

    /// <summary>
    ///     监控指定设备的心跳状态。
    /// </summary>
    public async Task MonitorAsync(DeviceConfig config, CancellationToken ct = default)
    {
        await Task.Yield();
        var lastOk = false;
        var isFirstCheck = true; // 标记是否为首次检测
        ushort writeData = 0;

        _logger.LogInformation("{PlcCode}-开始心跳监控，目标地址: {Host}:{Port}，心跳寄存器: {Register}，检测间隔: {Interval}ms",
            config.PlcCode, config.Host, config.Port, config.HeartbeatMonitorRegister, config.HeartbeatPollingInterval);

        while (!ct.IsCancellationRequested)
            try
            {
                if (!_plcLifecycle.TryGetClient(config.PlcCode, out _))
                {
                    _plcConnectionHealth[config.PlcCode] = false;
                    if (isFirstCheck || lastOk) _logger.LogWarning("{PlcCode}-未找到PLC客户端", config.PlcCode);
                    lastOk = false;
                    isFirstCheck = false;
                    await Task.Delay(config.HeartbeatPollingInterval, ct).ConfigureAwait(false);
                    continue;
                }

                // 先尝试写入心跳寄存器（这是最直接的连接测试）
                var connect = await WriteAsync(config.PlcCode, config.HeartbeatMonitorRegister, writeData, ct)
                    .ConfigureAwait(false);
                var ok = connect.IsSuccess;

                if (ok)
                {
                    writeData ^= 1;
                    _plcConnectionHealth[config.PlcCode] = true;
                    _lastErrors.TryRemove(config.PlcCode, out _); // 清除错误信息

                    // 首次检测成功或从失败状态恢复时记录日志
                    if (isFirstCheck || !lastOk)
                    {
                        _lastConnectedTimes[config.PlcCode] = DateTimeOffset.Now;
                        _logger.LogInformation("{PlcCode}-✓ PLC连接成功，心跳检测正常 (地址: {Host}:{Port}, 寄存器: {Register})",
                            config.PlcCode, config.Host, config.Port, config.HeartbeatMonitorRegister);
                        _metricsCollector?.RecordConnectionStatus(config.PlcCode, true);
                        RecordConnectionStart(config.PlcCode);
                    }
                }
                else
                {
                    _plcConnectionHealth[config.PlcCode] = false;
                    _lastErrors[config.PlcCode] = connect.Message; // 记录错误信息

                    // 首次检测失败或从成功状态变为失败时记录日志
                    if (isFirstCheck || lastOk)
                    {
                        _logger.LogWarning("{PlcCode}-✗ PLC连接失败: {Message} (地址: {Host}:{Port}, 寄存器: {Register})",
                            config.PlcCode, connect.Message, config.Host, config.Port, config.HeartbeatMonitorRegister);
                        _metricsCollector?.RecordConnectionStatus(config.PlcCode, false);
                        if (lastOk) RecordConnectionEnd(config.PlcCode);
                    }
                }

                lastOk = ok;
                isFirstCheck = false;
            }
            catch (Exception ex)
            {
                _plcConnectionHealth[config.PlcCode] = false;
                _lastErrors[config.PlcCode] = ex.Message; // 记录异常信息
                if (isFirstCheck || lastOk)
                    _logger.LogError(ex, "{PlcCode}-心跳检测异常: {Message}", config.PlcCode, ex.Message);
                lastOk = false;
                isFirstCheck = false;
            }
            finally
            {
                await Task.Delay(config.HeartbeatPollingInterval, ct).ConfigureAwait(false);
            }
    }

    /// <summary>
    ///     获取 PLC 连接状态。
    /// </summary>
    public bool TryGetConnectionHealth(string plcCode, out bool isConnected)
    {
        return _plcConnectionHealth.TryGetValue(plcCode, out isConnected);
    }

    /// <summary>
    ///     获取 PLC 连接详细信息。
    /// </summary>
    public PlcConnectionStatus? GetConnectionStatus(string plcCode)
    {
        if (!_plcConnectionHealth.TryGetValue(plcCode, out var isConnected))
            return null;

        var lastConnectedTime = _lastConnectedTimes.TryGetValue(plcCode, out var time) ? time : (DateTimeOffset?)null;
        var lastError = _lastErrors.TryGetValue(plcCode, out var error) ? error : null;
        
        double? connectionDuration = null;
        if (isConnected && _connectionStartTimes.TryGetValue(plcCode, out var startTime))
        {
            connectionDuration = (DateTime.Now - startTime).TotalSeconds;
        }

        return new PlcConnectionStatus
        {
            PlcCode = plcCode,
            IsConnected = isConnected,
            LastConnectedTime = lastConnectedTime,
            ConnectionDurationSeconds = connectionDuration,
            LastError = lastError
        };
    }

    /// <summary>
    ///     记录连接开始时间
    /// </summary>
    private void RecordConnectionStart(string plcCode)
    {
        _connectionStartTimes[plcCode] = DateTime.Now;
    }

    /// <summary>
    ///     记录连接结束并计算持续时间
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
    ///     向 PLC 写入心跳测试值。
    /// </summary>
    private async Task<PlcWriteResult> WriteAsync(string plcCode, string address, ushort value, CancellationToken ct)
    {
        if (!_plcLifecycle.TryGetClient(plcCode, out var client))
            return new PlcWriteResult
            {
                IsSuccess = false,
                Message = $"未找到 PLC {plcCode}"
            };

        if (!_plcLifecycle.TryGetLock(plcCode, out var locker))
            return new PlcWriteResult
            {
                IsSuccess = false,
                Message = $"未找到 PLC {plcCode} 的锁对象"
            };

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