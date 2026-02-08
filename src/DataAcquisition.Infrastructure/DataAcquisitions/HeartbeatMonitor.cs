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
///     心跳监控器，周期性检测 Plc 连通性。
///     连接恢复由下次心跳检测自动完成，无需额外重连逻辑。
/// </summary>
public class HeartbeatMonitor : IHeartbeatMonitor
{
    private readonly ConcurrentDictionary<string, DateTime> _connectionStartTimes = new();
    private readonly ILogger<HeartbeatMonitor> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly ConcurrentDictionary<string, bool> _plcConnectionHealth = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastConnectedTimes = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastDisconnectedTimes = new();
    private readonly ConcurrentDictionary<string, int> _reconnectCounts = new();
    private readonly ConcurrentDictionary<string, string?> _lastErrors = new();
    private readonly IPlcClientLifecycleService _plcLifecycle;

    public HeartbeatMonitor(IPlcClientLifecycleService plcLifecycle, ILogger<HeartbeatMonitor> logger,
        IMetricsCollector? metricsCollector = null)
    {
        _plcLifecycle = plcLifecycle;
        _logger = logger;
        _metricsCollector = metricsCollector;
    }

    public async Task MonitorAsync(DeviceConfig config, IPlcClientService client, CancellationToken ct = default)
    {
        var lastOk = false;
        ushort writeData = 0;

        _logger.LogInformation("{PlcCode}-开始心跳监控，目标地址: {Host}:{Port}，心跳寄存器: {Register}，检测间隔: {Interval}ms",
            config.PlcCode, config.Host, config.Port, config.HeartbeatMonitorRegister, config.HeartbeatPollingInterval);

        while (!ct.IsCancellationRequested)
            try
            {
                // 直接使用已获取的客户端实例写入心跳寄存器
                var connect = await client.WriteUShortAsync(config.HeartbeatMonitorRegister, writeData)
                    .ConfigureAwait(false);
                var ok = connect.IsSuccess;

                if (ok)
                {
                    writeData ^= 1;
                    _plcConnectionHealth[config.PlcCode] = true;
                    _lastErrors.TryRemove(config.PlcCode, out _); // 清除错误信息

                    // 从失败状态恢复时记录日志
                    if (!lastOk)
                    {
                        _lastConnectedTimes[config.PlcCode] = DateTimeOffset.Now;
                        _reconnectCounts.AddOrUpdate(config.PlcCode, 1, (_, c) => c + 1);
                        _logger.LogInformation("{PlcCode}-✓ Plc连接成功，心跳检测正常 (地址: {Host}:{Port}, 寄存器: {Register})",
                            config.PlcCode, config.Host, config.Port, config.HeartbeatMonitorRegister);
                        _metricsCollector?.RecordConnectionStatus(config.PlcCode, true);
                        _connectionStartTimes[config.PlcCode] = DateTime.Now;
                    }
                }
                else
                {
                    _plcConnectionHealth[config.PlcCode] = false;
                    _lastErrors[config.PlcCode] = connect.Message; // 记录错误信息

                    // 从成功状态变为失败时记录日志
                    if (lastOk)
                    {
                        _lastDisconnectedTimes[config.PlcCode] = DateTimeOffset.Now;
                        _logger.LogWarning("{PlcCode}-✗ Plc连接失败: {Message} (地址: {Host}:{Port}, 寄存器: {Register})",
                            config.PlcCode, connect.Message, config.Host, config.Port, config.HeartbeatMonitorRegister);
                        _metricsCollector?.RecordConnectionStatus(config.PlcCode, false);
                        if (_connectionStartTimes.TryRemove(config.PlcCode, out var startTime))
                            _metricsCollector?.RecordConnectionDuration(config.PlcCode, (DateTime.Now - startTime).TotalSeconds);
                    }
                }

                lastOk = ok;
            }
            catch (Exception ex)
            {
                _plcConnectionHealth[config.PlcCode] = false;
                _lastErrors[config.PlcCode] = ex.Message; // 记录异常信息
                if (lastOk)
                    _logger.LogError(ex, "{PlcCode}-心跳检测异常: {Message}", config.PlcCode, ex.Message);
                lastOk = false;
            }
            finally
            {
                await Task.Delay(config.HeartbeatPollingInterval, ct).ConfigureAwait(false);
            }
    }

    public bool TryGetConnectionHealth(string plcCode, out bool isConnected)
    {
        return _plcConnectionHealth.TryGetValue(plcCode, out isConnected);
    }

    public PlcConnectionStatus? GetConnectionStatus(string plcCode)
    {
        if (!_plcConnectionHealth.TryGetValue(plcCode, out var isConnected))
            return null;

        var lastConnectedTime = _lastConnectedTimes.TryGetValue(plcCode, out var time) ? time : (DateTimeOffset?)null;
        var lastDisconnectedTime = _lastDisconnectedTimes.TryGetValue(plcCode, out var dTime) ? dTime : (DateTimeOffset?)null;
        var reconnectCount = _reconnectCounts.TryGetValue(plcCode, out var rc) ? rc : 0;
        var lastError = _lastErrors.TryGetValue(plcCode, out var error) ? error : null;

        double? connectionDuration = null;
        if (isConnected && _connectionStartTimes.TryGetValue(plcCode, out var startTime))
        {
            connectionDuration = (DateTime.Now - startTime).TotalSeconds;
        }

        double? disconnectedDuration = null;
        if (!isConnected && lastDisconnectedTime.HasValue)
        {
            disconnectedDuration = (DateTimeOffset.Now - lastDisconnectedTime.Value).TotalSeconds;
        }

        return new PlcConnectionStatus
        {
            PlcCode = plcCode,
            IsConnected = isConnected,
            LastConnectedTime = lastConnectedTime,
            ConnectionDurationSeconds = connectionDuration,
            LastError = lastError,
            DisconnectedDurationSeconds = disconnectedDuration,
            TotalReconnectCount = reconnectCount,
            LastDisconnectedTime = lastDisconnectedTime
        };
    }
}