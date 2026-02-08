using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
///     数据采集任务编排器。管理 PLC 运行时生命周期（启动/停止/热更新）、心跳和通道采集任务。
/// </summary>
public class DataAcquisitionService : IDataAcquisitionService
{
    private readonly IChannelCollector _channelCollector;
    private readonly IDeviceConfigService _deviceConfigService;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ILogger<DataAcquisitionService> _logger;
    private readonly IPlcClientLifecycleService _plcLifecycle;
    private readonly IQueueService _queue;
    private readonly ConcurrentDictionary<string, PlcRuntime> _runtimes = new();
    private bool _disposed;

    public DataAcquisitionService(IDeviceConfigService deviceConfigService,
        IPlcClientLifecycleService plcLifecycle,
        ILogger<DataAcquisitionService> logger,
        IQueueService queue,
        IHeartbeatMonitor heartbeatMonitor,
        IChannelCollector channelCollector)
    {
        _deviceConfigService = deviceConfigService;
        _plcLifecycle = plcLifecycle;
        _logger = logger;
        _queue = queue;
        _heartbeatMonitor = heartbeatMonitor;
        _channelCollector = channelCollector;

        // 订阅配置变更事件
        _deviceConfigService.ConfigChanged += OnConfigChanged;
    }

    public async Task StartCollectionTasks()
    {
        var dataAcquisitionConfigs = await _deviceConfigService.GetConfigs().ConfigureAwait(false);
        foreach (var config in dataAcquisitionConfigs.Where(config => config.IsEnabled)) StartCollectionTask(config);
    }

    public async Task StopCollectionTasks()
    {
        try
        {
            // 先取消所有任务
            foreach (var kvp in _runtimes)
            {
                try { await kvp.Value.Cts.CancelAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogError(ex, "取消采集任务失败: {Message}", ex.Message); }
            }

            // 等待所有任务完成并释放 CTS
            foreach (var kvp in _runtimes)
            {
                try { await kvp.Value.Running.ConfigureAwait(false); }
                catch (OperationCanceledException) { /* 预期的取消 */ }
                catch (Exception ex) { _logger.LogError(ex, "等待任务完成失败: {Message}", ex.Message); }
                finally { kvp.Value.Cts.Dispose(); }
            }

            await _plcLifecycle.CloseAllAsync().ConfigureAwait(false);
            await _queue.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _runtimes.Clear();
        }
    }

    public async Task<PlcWriteResult> WritePlcAsync(string plcCode, string address, object value,
        string dataType, CancellationToken ct = default)
    {
        var configs = await _deviceConfigService.GetConfigs().ConfigureAwait(false);
        var config = configs.FirstOrDefault(c => c.PlcCode == plcCode);
        if (config == null)
            return new PlcWriteResult
            {
                IsSuccess = false,
                Message = $"未找到 Plc {plcCode} 的配置"
            };

        var client = _plcLifecycle.GetOrCreateClient(config);
        return dataType switch
        {
            "ushort" => await client.WriteUShortAsync(address, Convert.ToUInt16(value)).ConfigureAwait(false),
            "uint" => await client.WriteUIntAsync(address, Convert.ToUInt32(value)).ConfigureAwait(false),
            "ulong" => await client.WriteULongAsync(address, Convert.ToUInt64(value)).ConfigureAwait(false),
            "short" => await client.WriteShortAsync(address, Convert.ToInt16(value)).ConfigureAwait(false),
            "int" => await client.WriteIntAsync(address, Convert.ToInt32(value)).ConfigureAwait(false),
            "long" => await client.WriteLongAsync(address, Convert.ToInt64(value)).ConfigureAwait(false),
            "float" => await client.WriteFloatAsync(address, Convert.ToSingle(value)).ConfigureAwait(false),
            "double" => await client.WriteDoubleAsync(address, Convert.ToDouble(value)).ConfigureAwait(false),
            "string" => await client.WriteStringAsync(address, Convert.ToString(value) ?? string.Empty)
                .ConfigureAwait(false),
            "bool" => await client.WriteBoolAsync(address, Convert.ToBoolean(value)).ConfigureAwait(false),
            _ => new PlcWriteResult { IsSuccess = false, Message = $"不支持的数据类型: {dataType}" }
        };
    }

    public IReadOnlyCollection<PlcConnectionStatus> GetPlcConnections()
    {
        return _runtimes.Keys
            .Select(plcCode => _heartbeatMonitor.GetConnectionStatus(plcCode))
            .Where(s => s != null)
            .OrderBy(s => s!.PlcCode)
            .ToList()!;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _deviceConfigService.ConfigChanged -= OnConfigChanged;
        StopCollectionTasks().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>启动单个采集任务，已存在则跳过。</summary>
    private void StartCollectionTask(DeviceConfig config)
    {
        // 前置校验
        if (string.IsNullOrWhiteSpace(config.PlcCode))
        {
            _logger.LogError("启动采集任务失败：设备编码为空");
            return;
        }

        if (config.Channels.Count == 0)
        {
            _logger.LogError("启动采集任务失败：设备 {PlcCode} 没有配置采集通道", config.PlcCode);
            return;
        }

        // 任务已存在则跳过
        if (_runtimes.ContainsKey(config.PlcCode))
            return;

        var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var client = _plcLifecycle.GetOrCreateClient(config);

        var tasks = new List<Task>(config.Channels.Count + 1)
        {
            _heartbeatMonitor.MonitorAsync(config, client, ct)
        };
        foreach (var channel in config.Channels)
            tasks.Add(_channelCollector.CollectAsync(config, channel, client, ct));

        var running = Task.WhenAll(tasks);
        _ = running.ContinueWith(t =>
        {
            var ex = t.Exception?.Flatten().InnerException;
            if (ex != null)
                _logger.LogError(ex, "{PlcCode}-采集任务异常: {Message}", config.PlcCode, ex.Message);
        }, TaskContinuationOptions.OnlyOnFaulted);

        var runtime = new PlcRuntime(cts, running);
        // 原子性写入；如果并发竞争失败（极少见），释放 cts
        if (!_runtimes.TryAdd(config.PlcCode, runtime))
        {
            cts.Dispose();
        }
    }

    /// <summary>配置变更事件处理。async void 中异常必须完全捕获。</summary>
    private async void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        try
        {
            switch (e.ChangeType)
            {
                case ConfigChangeType.Added:
                    if (e.NewConfig is { IsEnabled: true })
                    {
                        _logger.LogInformation("检测到新设备配置: {PlcCode}，启动采集任务", e.PlcCode);
                        StartCollectionTask(e.NewConfig);
                    }

                    break;

                case ConfigChangeType.Updated:
                    if (e.OldConfig != null) await StopCollectionTaskAsync(e.OldConfig.PlcCode).ConfigureAwait(false);
                    if (e.NewConfig is { IsEnabled: true })
                    {
                        _logger.LogInformation("设备配置已更新: {PlcCode}，重启采集任务", e.PlcCode);
                        StartCollectionTask(e.NewConfig);
                    }

                    break;

                case ConfigChangeType.Removed:
                    if (e.OldConfig != null)
                    {
                        _logger.LogInformation("设备配置已删除: {PlcCode}，停止采集任务", e.PlcCode);
                        await StopCollectionTaskAsync(e.OldConfig.PlcCode).ConfigureAwait(false);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            // async void 方法中的异常必须完全捕获，否则可能导致应用程序崩溃
            try
            {
                _logger.LogError(ex, "处理配置变更失败: {Message}", ex.Message);
            }
            catch
            {
                // 如果日志记录也失败，静默处理（避免崩溃）
                // 在实际生产环境中，可以考虑写入系统事件日志或使用其他故障安全机制
            }
        }
    }

    private async Task StopCollectionTaskAsync(string plcCode)
    {
        if (!_runtimes.TryRemove(plcCode, out var runtime)) return;

        try
        {
            await runtime.Cts.CancelAsync().ConfigureAwait(false);
            try
            {
                await runtime.Running.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 预期的取消异常，忽略
            }
            finally
            {
                runtime.Cts.Dispose();
            }

            // 关闭并清理 Plc 客户端与锁
            await _plcLifecycle.CloseAsync(plcCode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止采集任务失败 {PlcCode}: {Message}", plcCode, ex.Message);
        }
    }
}