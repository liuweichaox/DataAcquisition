using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using DataAcquisition.Application;
using DataAcquisition.Domain.Clients;

namespace DataAcquisition.Infrastructure.DataAcquisitions
{
    /// <summary>
    /// 数据采集器实现
    /// </summary>
    public class DataAcquisitionService : IDataAcquisitionService
    {
        private readonly IPLCStateManager _plcStateManager;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly IPLCClientLifecycleService _plcLifecycle;
        private readonly IOperationalEventsService _events;
        private readonly IQueueService _queue;
        private readonly IHeartbeatMonitor _heartbeatMonitor;
        private readonly IChannelCollector _channelCollector;

        /// <summary>
        /// 数据采集器
        /// </summary>
        public DataAcquisitionService(IDeviceConfigService deviceConfigService,
            IPLCClientLifecycleService plcLifecycle,
            IOperationalEventsService events,
            IQueueService queue,
            IPLCStateManager plcStateManager,
            IHeartbeatMonitor heartbeatMonitor,
            IChannelCollector channelCollector)
        {
            _deviceConfigService = deviceConfigService;
            _plcLifecycle = plcLifecycle;
            _events = events;
            _queue = queue;
            _plcStateManager = plcStateManager;
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
            if (_plcStateManager.Runtimes.ContainsKey(config.Code))
            {
                return;
            }

            // 防御性检查：确保配置有效
            if (config == null)
            {
                _events.ErrorAsync("启动采集任务失败：配置为 null", null).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(config.Code))
            {
                _events.ErrorAsync("启动采集任务失败：设备编码为空", null).ConfigureAwait(false);
                return;
            }

            if (config.Channels == null || config.Channels.Count == 0)
            {
                _events.ErrorAsync($"启动采集任务失败：设备 {config.Code} 没有配置采集通道", null).ConfigureAwait(false);
                return;
            }

            _plcStateManager.PlcConnectionHealth[config.Code] = false;

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var client = _plcLifecycle.GetOrCreateClient(config);

            var tasks = new List<Task> { _heartbeatMonitor.MonitorAsync(config, ct) };

            foreach (var channel in config.Channels)
            {
                tasks.Add(_channelCollector.CollectAsync(config, channel, client, ct));
            }

            var running = Task.WhenAll(tasks);
            _ = running.ContinueWith(async t =>
            {
                if (t.Exception != null)
                {
                    var innerException = t.Exception.Flatten().InnerException;
                    await _events.ErrorAsync($"{config.Code}-采集任务异常: {innerException?.Message}", innerException).ConfigureAwait(false);
                }
            }, TaskContinuationOptions.OnlyOnFaulted).Unwrap();

            _plcStateManager.Runtimes.TryAdd(config.Code, new PlcRuntime(cts, running));
        }

        /// <summary>
        /// 停止所有数据采集任务并释放相关资源
        /// </summary>
        public async Task StopCollectionTasks()
        {
            try
            {
                // Cancel the data acquisition tasks.
                foreach (var kvp in _plcStateManager.Runtimes)
                {
                    try
                    {
                        await kvp.Value.Cts.CancelAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await _events.ErrorAsync($"取消采集任务失败: {ex.Message}", ex).ConfigureAwait(false);
                    }
                }

                foreach (var kv in _plcStateManager.Runtimes)
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
                        await _events.ErrorAsync($"等待任务完成失败: {ex.Message}", ex).ConfigureAwait(false);
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
                _plcStateManager.Clear();
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
        public async Task<PlcWriteResult> WritePlcAsync(string plcCode, string address, object value,
            string dataType, CancellationToken ct = default)
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
                    _ => new PlcWriteResult { IsSuccess = false, Message = $"不支持的数据类型: {dataType}" }
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
            return new SortedDictionary<string, bool>(_plcStateManager.PlcConnectionHealth);
        }

        /// <summary>
        /// 配置变更处理
        /// </summary>
        private async void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
        {
            try
            {
                switch (e.ChangeType)
                {
                    case ConfigChangeType.Added:
                        if (e.NewConfig != null && e.NewConfig.IsEnabled)
                        {
                            await _events.InfoAsync($"检测到新设备配置: {e.DeviceCode}，启动采集任务").ConfigureAwait(false);
                            StartCollectionTask(e.NewConfig);
                        }
                        break;

                    case ConfigChangeType.Updated:
                        if (e.OldConfig != null)
                        {
                            await StopCollectionTaskAsync(e.OldConfig.Code).ConfigureAwait(false);
                        }
                        if (e.NewConfig != null && e.NewConfig.IsEnabled)
                        {
                            await _events.InfoAsync($"设备配置已更新: {e.DeviceCode}，重启采集任务").ConfigureAwait(false);
                            StartCollectionTask(e.NewConfig);
                        }
                        break;

                    case ConfigChangeType.Removed:
                        if (e.OldConfig != null)
                        {
                            await _events.InfoAsync($"设备配置已删除: {e.DeviceCode}，停止采集任务").ConfigureAwait(false);
                            await StopCollectionTaskAsync(e.OldConfig.Code).ConfigureAwait(false);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                await _events.ErrorAsync($"处理配置变更失败: {ex.Message}", ex).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 停止单个采集任务
        /// </summary>
        private async Task StopCollectionTaskAsync(string deviceCode)
        {
            if (!_plcStateManager.Runtimes.TryRemove(deviceCode, out var runtime))
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
                await _plcLifecycle.CloseAsync(deviceCode).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _events.ErrorAsync($"停止采集任务失败 {deviceCode}: {ex.Message}", ex).ConfigureAwait(false);
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
}