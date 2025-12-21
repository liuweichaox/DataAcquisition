using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Clients;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
/// 数据采集器实现
/// </summary>
public class DataAcquisitionService : IDataAcquisitionService
{
    private readonly ConcurrentDictionary<string, PLCRuntime> _runtimes = new();
    private readonly IDeviceConfigService _deviceConfigService;
    private readonly IPLCClientLifecycleService _plcLifecycle;
    private readonly ILogger<DataAcquisitionService> _logger;
    private readonly IQueueService _queue;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly IChannelCollector _channelCollector;

    /// <summary>
    /// 数据采集器
    /// </summary>
    public DataAcquisitionService(IDeviceConfigService deviceConfigService,
        IPLCClientLifecycleService plcLifecycle,
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

    /// <summary>
    /// 开始所有采集任务
    /// </summary>
    public async Task StartCollectionTasks()
    {
        var dataAcquisitionConfigs = await _deviceConfigService.GetConfigs().ConfigureAwait(false);
        foreach (var config in dataAcquisitionConfigs.Where(config => config.IsEnabled))
        {
            StartCollectionTask(config);
        }
    }

    /// <summary>
    /// 启动单个采集任务（如果任务已存在则直接返回）
    /// </summary>
    private void StartCollectionTask(DeviceConfig config)
    {
        // 使用 TryAdd 原子操作检查并添加，避免竞态条件
        if (!_runtimes.TryAdd(config.PLCCode, null!))
        {
            // 任务已存在，移除刚添加的 null 值（这种情况不应该发生，但防御性编程）
            if (_runtimes.TryGetValue(config.PLCCode, out var existingRuntime) && existingRuntime != null)
            {
                return;
            }
            // 如果值为 null（不应该发生），继续执行创建流程
            _runtimes.TryRemove(config.PLCCode, out _);
        }

        if (string.IsNullOrWhiteSpace(config.PLCCode))
        {
            _logger.LogError("启动采集任务失败：设备编码为空");
            return;
        }

        if (config.Channels.Count == 0)
        {
            _logger.LogError("启动采集任务失败：设备 {PLCCode} 没有配置采集通道", config.PLCCode);
            return;
        }


        var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var client = _plcLifecycle.GetOrCreateClient(config);

        var tasks = new List<Task> { _heartbeatMonitor.MonitorAsync(config, ct) };

        foreach (var channel in config.Channels)
        {
            tasks.Add(_channelCollector.CollectAsync(config, channel, client, ct));
        }

        var running = Task.WhenAll(tasks);
        _ = running.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                var innerException = t.Exception.Flatten().InnerException;
                _logger.LogError(innerException, "{PLCCode}-采集任务异常: {Message}", config.PLCCode, innerException?.Message);
            }

            return Task.CompletedTask;
        }, TaskContinuationOptions.OnlyOnFaulted).Unwrap();

        // 更新运行时对象（之前已用 TryAdd 占位）
        var runtime = new PLCRuntime(cts, running);
        _runtimes.TryUpdate(config.PLCCode, runtime, null!);
    }

    /// <summary>
    /// 停止所有数据采集任务并释放相关资源
    /// </summary>
    public async Task StopCollectionTasks()
    {
        try
        {
            // Cancel the data acquisition tasks.
            foreach (var kvp in _runtimes)
            {
                try
                {
                    await kvp.Value.Cts.CancelAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "取消采集任务失败: {Message}", ex.Message);
                }
            }

            foreach (var kv in _runtimes)
            {
                try
                {
                    await kv.Value.Running.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 预期的取消异常，忽略
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "等待任务完成失败: {Message}", ex.Message);
                }
                finally
                {
                    kv.Value.Cts.Dispose();
                }
            }

            // 关闭并清理所有 PLC 客户端与锁
            await _plcLifecycle.CloseAllAsync().ConfigureAwait(false);

            // Complete and dispose the queue.
            await _queue.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _runtimes.Clear();
        }
    }

    /// <summary>
    /// 写入 PLC 寄存器
    /// </summary>
    /// <param name="plcCode">PLC 编号</param>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="ct"></param>
    /// <returns>写入结果</returns>
    public async Task<PLCWriteResult> WritePLCAsync(string plcCode, string address, object value,
        string dataType, CancellationToken ct = default)
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
                "string" => await client.WriteStringAsync(address, Convert.ToString(value) ?? string.Empty).ConfigureAwait(false),
                "bool" => await client.WriteBoolAsync(address, Convert.ToBoolean(value)).ConfigureAwait(false),
                _ => new PLCWriteResult { IsSuccess = false, Message = $"不支持的数据类型: {dataType}" }
            };
        }
        finally
        {
            locker.Release();
        }
    }

    /// <summary>
    /// 获取当前所有 PLC 连接状态
    /// </summary>
    public SortedDictionary<string, bool> GetPlcConnectionStatus()
    {
        // 从 HeartbeatMonitor 获取连接状态
        var connectionHealth = new SortedDictionary<string, bool>();
        foreach (var runtime in _runtimes)
        {
            if (_heartbeatMonitor.TryGetConnectionHealth(runtime.Key, out var isConnected))
            {
                connectionHealth[runtime.Key] = isConnected;
            }
        }
        return connectionHealth;
    }

    /// <summary>
    /// 配置变更处理（异步事件处理器）。
    /// 注意：使用 async void 是事件处理器的标准模式，但异常必须完全捕获。
    /// </summary>
    private async void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        try
        {
            switch (e.ChangeType)
            {
                case ConfigChangeType.Added:
                    if (e.NewConfig is { IsEnabled: true })
                    {
                        _logger.LogInformation("检测到新设备配置: {PLCCode}，启动采集任务", e.PLCCode);
                        StartCollectionTask(e.NewConfig);
                    }
                    break;

                case ConfigChangeType.Updated:
                    if (e.OldConfig != null)
                    {
                        await StopCollectionTaskAsync(e.OldConfig.PLCCode).ConfigureAwait(false);
                    }
                    if (e.NewConfig is { IsEnabled: true })
                    {
                        _logger.LogInformation("设备配置已更新: {PLCCode}，重启采集任务", e.PLCCode);
                        StartCollectionTask(e.NewConfig);
                    }
                    break;

                case ConfigChangeType.Removed:
                    if (e.OldConfig != null)
                    {
                        _logger.LogInformation("设备配置已删除: {PLCCode}，停止采集任务", e.PLCCode);
                        await StopCollectionTaskAsync(e.OldConfig.PLCCode).ConfigureAwait(false);
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

    /// <summary>
    /// 停止单个采集任务
    /// </summary>
    private async Task StopCollectionTaskAsync(string plcCode)
    {
        if (!_runtimes.TryRemove(plcCode, out var runtime))
        {
            return;
        }

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

            // 关闭并清理 PLC 客户端与锁
            await _plcLifecycle.CloseAsync(plcCode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止采集任务失败 {PLCCode}: {Message}", plcCode, ex.Message);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 使用 ConfigureAwait(false) 避免死锁
        StopCollectionTasks().ConfigureAwait(false).GetAwaiter().GetResult();
    }
}