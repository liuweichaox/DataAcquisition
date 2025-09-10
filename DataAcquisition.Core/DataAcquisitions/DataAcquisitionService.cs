using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Core.Communication;
using DataAcquisition.Core.DeviceConfigs;
using DataAcquisition.Core.OperationalEvents;

namespace DataAcquisition.Core.DataAcquisitions
{    
    public sealed record PlcRuntime(CancellationTokenSource Cts, Task Running);
    /// <summary>
    /// 数据采集器实现
    /// </summary>
    public class DataAcquisitionService : IDataAcquisitionService
    {
        private readonly PlcStateManager _plcStateManager;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly ICommunicationFactory _communicationFactory;
        private readonly IOperationalEvents _events;
        private readonly IQueue _queue;
        private readonly ConcurrentDictionary<string, DateTime> _lastStartTimes = new();
        private readonly ConcurrentDictionary<string, string> _lastStartTimeColumns = new();

        /// <summary>
        /// 数据采集器
        /// </summary>
        public DataAcquisitionService(IDeviceConfigService deviceConfigService,
            ICommunicationFactory communicationFactory,
            IOperationalEvents events,
            IQueue queue)
        {
            _plcStateManager = new PlcStateManager();
            _deviceConfigService = deviceConfigService;
            _communicationFactory = communicationFactory;
            _events = events;
            _queue = queue;
        }

        /// <summary>
        /// 内部类管理 PLC 状态
        /// </summary>
        private class PlcStateManager
        {
            /// <summary>
            /// Runtime information for each PLC task.
            /// </summary>
            public readonly ConcurrentDictionary<string, PlcRuntime> Runtimes = new();

            /// <summary>
            /// Communication client associated with every PLC.
            /// </summary>
            public ConcurrentDictionary<string, ICommunication> PlcClients { get; } = new();

            /// <summary>
            /// Connection status for each PLC.
            /// </summary>
            public ConcurrentDictionary<string, bool> PlcConnectionHealth { get; } = new();

            /// <summary>
            /// Lock objects used to prevent concurrent access per PLC.
            /// </summary>
            public readonly ConcurrentDictionary<string, SemaphoreSlim> PlcLocks = new();

            public void Clear()
            {
                PlcClients.Clear();
                PlcConnectionHealth.Clear();
                Runtimes.Clear();
                PlcLocks.Clear();
            }
        }
        
        /// <summary>
        /// 开始所有采集任务
        /// </summary>
        public async Task StartCollectionTasks()
        {
            var dataAcquisitionConfigs = await _deviceConfigService.GetConfigs();
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

            _plcStateManager.PlcConnectionHealth[config.Code] = false;

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var client = CreateCommunicationClient(config);
                    
            var tasks = new List<Task> { StartHeartbeatMonitor(config, ct) };

            foreach (var module in config.Modules)
            {
                tasks.Add(ModuleLoopAsync(config, module, client, ct));
            }
                    
            var running = Task.WhenAll(tasks);
            _ = running.ContinueWith(async t =>
            {
                if (t.Exception != null)
                    await _events.ErrorAsync(config.Code, $"采集任务异常: {t.Exception.Flatten().InnerException?.Message}", t.Exception.Flatten().InnerException);
            }, TaskContinuationOptions.OnlyOnFaulted);
                    
            _plcStateManager.Runtimes.TryAdd(config.Code, new PlcRuntime(cts, running));
        }

        private async Task ModuleLoopAsync(DeviceConfig config, Module module, ICommunication client,
            CancellationToken ct = default)
        {
            await Task.Yield();
            object? prevVal = null;
            // Continue acquiring data until cancellation is requested.
            while (!ct.IsCancellationRequested)
            {
                // Proceed only if the PLC is connected.
                if (!_plcStateManager.PlcConnectionHealth.TryGetValue(config.Code,
                        out var isConnected) || !isConnected)
                {
                    await Task.Delay(config.HeartbeatPollingInterval, ct);
                    continue;
                }

                var locker = _plcStateManager.PlcLocks[config.Code];
                await locker.WaitAsync(ct);

                try
                {
                    var trigger = module.Trigger;

                    var currVal = trigger.Mode == TriggerMode.Always
                        ? null
                        : await ReadCommunicationValueAsync(client, trigger.Register, trigger.DataType);

                    if (!ShouldSample(trigger.Mode, prevVal, currVal))
                    {
                        await Task.Delay(100, ct); 
                        continue;
                    }

                    try
                    {
                        var operation = trigger.Operation;
                        var key = $"{config.Code}:{module.TableName}";
                        var timestamp = DateTime.Now;
                        var dataMessage = new DataMessage(timestamp, module.TableName, module.BatchSize,
                            module.DataPoints, operation);
                        if (operation == DataOperation.Insert)
                        {
                            var batchData = await client.ReadAsync(module.BatchReadRegister, module.BatchReadLength);
                            var buffer = batchData.Content;
                            if (module.DataPoints != null)
                            {
                                foreach (var dataPoint in module.DataPoints)
                                {
                                    var value = TransValue(client, buffer, dataPoint.Index,
                                        dataPoint.StringByteLength, dataPoint.DataType,
                                        dataPoint.Encoding);
                                    dataMessage.DataValues[dataPoint.ColumnName] = value;
                                }
                            }

                            if (!string.IsNullOrEmpty(trigger.TimeColumnName))
                            {
                                dataMessage.DataValues[trigger.TimeColumnName] = timestamp;
                                _lastStartTimes[key] = timestamp;
                                _lastStartTimeColumns[key] = trigger.TimeColumnName;
                            }

                            await _queue.PublishAsync(dataMessage);
                        }
                        else if (_lastStartTimes.TryRemove(key, out var startTime))
                        {
                            if (_lastStartTimeColumns.TryRemove(key, out var startColumn))
                            {
                                dataMessage.KeyValues[startColumn] = startTime;
                            }

                            if (!string.IsNullOrEmpty(trigger.TimeColumnName))
                            {
                                dataMessage.DataValues[trigger.TimeColumnName] = timestamp;
                            }

                            await _queue.PublishAsync(dataMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        await _events.ErrorAsync(module.ChamberCode, $"[{module.ChamberCode}:{module.TableName}]采集异常: {ex.Message}", ex);
                    }

                    prevVal = currVal;
                }
                finally
                {
                    locker.Release();
                }
            }
        }

        /// <summary>
        /// 触发模式下是否采样
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="prev"></param>
        /// <param name="curr"></param>
        /// <returns></returns>
        private static bool ShouldSample(TriggerMode mode, object? prev, object? curr)
        {
            if (prev == null || curr == null) return true;
            var p = Convert.ToDecimal(prev);
            var c = Convert.ToDecimal(curr);
            return mode switch
            {
                TriggerMode.Always => true,
                TriggerMode.ValueIncrease => p < c,
                TriggerMode.ValueDecrease => p > c,
                TriggerMode.RisingEdge => p == 0 && c == 1,
                TriggerMode.FallingEdge => p == 1 && c == 0,
                _ => false
            };
        }

        /// <summary>
        /// 创建 PLC 客户端（若已存在则直接返回）
        /// </summary>
        private ICommunication CreateCommunicationClient(DeviceConfig config)
        {
            if (_plcStateManager.PlcClients.TryGetValue(config.Code, out var client))
            {
                return client;
            }

            client = _communicationFactory.Create(config);
            _plcStateManager.PlcClients.TryAdd(config.Code, client);
            _plcStateManager.PlcLocks.TryAdd(config.Code, new SemaphoreSlim(1, 1));
            return client;
        }

        /// <summary>
        /// 检查 PLC 连接状态，若断开则尝试重连
        /// </summary>
        private async Task StartHeartbeatMonitor(DeviceConfig config, CancellationToken ct = default)
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
                        var connect = await WritePlcAsync(config.Code, config.HeartbeatMonitorRegister, writeData,
                            "ushort", ct);
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
        /// 读取 PLC 值
        /// </summary>
        /// <param name="client"></param>
        /// <param name="register"></param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private static async Task<object> ReadCommunicationValueAsync(ICommunication client, string register,
            string dataType)
        {
            return dataType switch
            {
                "ushort" => await client.ReadUShortAsync(register),
                "uint" => await client.ReadUIntAsync(register),
                "ulong" => await client.ReadULongAsync(register),
                "short" => await client.ReadShortAsync(register),
                "int" => await client.ReadIntAsync(register),
                "long" => await client.ReadLongAsync(register),
                "float" => await client.ReadFloatAsync(register),
                "double" => await client.ReadDoubleAsync(register),
                _ => throw new NotSupportedException($"不支持的数据类型: {dataType}")
            };
        }


        private static dynamic? TransValue(ICommunication client, byte[] buffer, int index, int length, string dataType,
            string encoding)
        {
            return dataType.ToLower() switch
            {
                "ushort" => client.TransUShort(buffer, index),
                "uint" => client.TransUInt(buffer, index),
                "ulong" => client.TransULong(buffer, index),
                "short" => client.TransShort(buffer, index),
                "int" => client.TransInt(buffer, index),
                "long" => client.TransLong(buffer, index),
                "float" => client.TransFloat(buffer, index),
                "double" => client.TransDouble(buffer, index),
                "string" => client.TransString(buffer, index, length, Encoding.GetEncoding(encoding)),
                "bool" => client.TransBool(buffer, index),
                _ => null
            };
        }

        /// <summary>
        /// 停止所有数据采集任务并释放相关资源
        /// </summary>
        public async Task StopCollectionTasks()
        {
            // Cancel the data acquisition tasks.
            foreach (var kvp in _plcStateManager.Runtimes)
            {
                await kvp.Value.Cts.CancelAsync();
            }

            foreach (var kv in _plcStateManager.Runtimes)
            {
                try { await kv.Value.Running; } catch (OperationCanceledException) { }
                kv.Value.Cts.Dispose();
            }

            // Close and clean up all PLC clients.
            foreach (var client in _plcStateManager.PlcClients.Values)
            {
                await client.ConnectCloseAsync();
            }

            foreach (var sem in _plcStateManager.PlcLocks.Values)
            {
                sem.Dispose();
            }

            // Complete and dispose the queue.
            await _queue.DisposeAsync();

            _plcStateManager.Clear();
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
        public async Task<CommunicationWriteResult> WritePlcAsync(string plcCode, string address, object value,
            string dataType, CancellationToken ct = default)
        {
            if (!_plcStateManager.PlcClients.TryGetValue(plcCode, out var client))
            {
                return new CommunicationWriteResult
                {
                    IsSuccess = false,
                    Message = $"未找到 PLC {plcCode}"
                };
            }

            var locker = _plcStateManager.PlcLocks[plcCode];
            await locker.WaitAsync(ct);
            try
            {
                return dataType switch
                {
                    "ushort" => await client.WriteUShortAsync(address, Convert.ToUInt16(value)),
                    "uint" => await client.WriteUIntAsync(address, Convert.ToUInt32(value)),
                    "ulong" => await client.WriteULongAsync(address, Convert.ToUInt64(value)),
                    "short" => await client.WriteShortAsync(address, Convert.ToInt16(value)),
                    "int" => await client.WriteIntAsync(address, Convert.ToInt32(value)),
                    "long" => await client.WriteLongAsync(address, Convert.ToInt64(value)),
                    "float" => await client.WriteFloatAsync(address, Convert.ToSingle(value)),
                    "double" => await client.WriteDoubleAsync(address, Convert.ToDouble(value)),
                    "string" => await client.WriteStringAsync(address, Convert.ToString(value) ?? string.Empty),
                    "bool" => await client.WriteBoolAsync(address, Convert.ToBoolean(value)),
                    _ => new CommunicationWriteResult { IsSuccess = false, Message = $"不支持的数据类型: {dataType}" }
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
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopCollectionTasks().Wait();
        }
    }
}