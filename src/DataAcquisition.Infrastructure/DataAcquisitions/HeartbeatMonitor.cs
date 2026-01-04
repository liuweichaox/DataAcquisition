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
    private readonly Dictionary<string, DateTime> _disconnectionStartTimes = new();
    private readonly ILogger<HeartbeatMonitor> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly ConcurrentDictionary<string, bool> _plcConnectionHealth = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastConnectedTimes = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastDisconnectedTimes = new();
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
        ushort writeData = 0;

        _logger.LogInformation("{PlcCode}-开始心跳监控，目标地址: {Host}:{Port}，心跳寄存器: {Register}，检测间隔: {Interval}ms",
            config.PlcCode, config.Host, config.Port, config.HeartbeatMonitorRegister, config.HeartbeatPollingInterval);

        while (!ct.IsCancellationRequested)
            try
            {
                // 先尝试写入心跳寄存器（这是最直接的连接测试）
                var connect = await WriteAsync(config, config.HeartbeatMonitorRegister, writeData, ct)
                    .ConfigureAwait(false);
                var ok = connect.IsSuccess;

                if (ok)
                {
                    writeData ^= 1;
                    _plcConnectionHealth[config.PlcCode] = true;
                    _lastErrors.TryRemove(config.PlcCode, out _);

                    // 每次心跳成功都记录
                    _logger.LogDebug("设备 {PlcCode} 心跳正常 ✓", config.PlcCode);

                    // 从失败状态恢复时记录
                    if (!lastOk)
                    {
                        _lastConnectedTimes[config.PlcCode] = DateTimeOffset.Now;
                        _logger.LogInformation("设备 {PlcCode} 已恢复连接 ✓", config.PlcCode);
                        _metricsCollector?.RecordConnectionStatus(config.PlcCode, true);
                        RecordConnectionStart(config.PlcCode);
                    }
                }
                else
                {
                    _plcConnectionHealth[config.PlcCode] = false;
                    _lastErrors[config.PlcCode] = connect.Message;

                    // 每次心跳失败都记录
                    _logger.LogWarning("设备 {PlcCode} 心跳异常 ✗ | 错误：{ErrorMessage}",
                        config.PlcCode, connect.Message);

                    // 从成功状态变为失败时记录
                    if (lastOk)
                    {
                        _lastDisconnectedTimes[config.PlcCode] = DateTimeOffset.Now;
                        _logger.LogWarning("设备 {PlcCode} 连接断开 ✗", config.PlcCode);
                        _metricsCollector?.RecordConnectionStatus(config.PlcCode, false);
                        RecordConnectionEnd(config.PlcCode);
                    }

                    RecordDisconnectionStart(config.PlcCode);
                }

                lastOk = ok;
            }
            catch (Exception ex)
            {
                _plcConnectionHealth[config.PlcCode] = false;
                _lastErrors[config.PlcCode] = ex.Message;

                // 每次异常都记录
                _logger.LogError(ex, "设备 {PlcCode} 心跳检测异常 ✗ | 错误：{ErrorMessage}",
                    config.PlcCode, ex.Message);

                // 如果之前是连接状态，记录断开信息
                if (lastOk)
                {
                    _lastDisconnectedTimes[config.PlcCode] = DateTimeOffset.Now;
                    _metricsCollector?.RecordConnectionStatus(config.PlcCode, false);
                    RecordConnectionEnd(config.PlcCode);
                }

                RecordDisconnectionStart(config.PlcCode);
                lastOk = false;
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
        var lastDisconnectedTime = _lastDisconnectedTimes.TryGetValue(plcCode, out var disconnectedTime) ? disconnectedTime : (DateTimeOffset?)null;
        var lastError = _lastErrors.TryGetValue(plcCode, out var error) ? error : null;

        double? connectionDuration = null;
        if (isConnected && _connectionStartTimes.TryGetValue(plcCode, out var startTime))
        {
            connectionDuration = (DateTime.Now - startTime).TotalSeconds;
        }

        double? disconnectedDuration = null;
        if (!isConnected && _disconnectionStartTimes.TryGetValue(plcCode, out var disconnectionStart))
        {
            disconnectedDuration = (DateTime.Now - disconnectionStart).TotalSeconds;
        }

        return new PlcConnectionStatus
        {
            PlcCode = plcCode,
            IsConnected = isConnected,
            LastConnectedTime = lastConnectedTime,
            ConnectionDurationSeconds = connectionDuration,
            LastError = lastError,
            DisconnectedDurationSeconds = disconnectedDuration,
            TotalReconnectCount = 0,
            LastDisconnectedTime = lastDisconnectedTime
        };
    }

    /// <summary>
    ///     记录连接开始时间
    /// </summary>
    private void RecordConnectionStart(string plcCode)
    {
        _connectionStartTimes[plcCode] = DateTime.Now;
        _disconnectionStartTimes.Remove(plcCode);
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
    ///     记录断开开始时间
    /// </summary>
    private void RecordDisconnectionStart(string plcCode)
    {
        if (!_disconnectionStartTimes.ContainsKey(plcCode))
        {
            _disconnectionStartTimes[plcCode] = DateTime.Now;
        }
    }


    /// <summary>
    ///     向 PLC 写入心跳测试值。
    /// </summary>
    private async Task<PlcWriteResult> WriteAsync(DeviceConfig config, string address, ushort value, CancellationToken ct)
    {
        var client = _plcLifecycle.GetOrCreateClient(config);
        return await client.WriteUShortAsync(address, value).ConfigureAwait(false);
    }
}