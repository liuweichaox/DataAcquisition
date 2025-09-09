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
using DataAcquisition.Core.Messages;

namespace DataAcquisition.Core.DataAcquisitions
{

    /// <summary>
    /// 数据采集器实现
    /// </summary>
    public class DataAcquisitionService : IDataAcquisitionService
    {
        private readonly PlcStateManager _plcStateManager;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly ICommunicationFactory _communicationFactory;
        private readonly IMessage _message;
        private readonly IQueue _queue;
        private readonly ConcurrentDictionary<string, DateTime> _lastStartTimes = new();
        private readonly ConcurrentDictionary<string, string> _lastStartTimeColumns = new();

        /// <summary>
        /// 数据采集器
        /// </summary>
        public DataAcquisitionService(IDeviceConfigService deviceConfigService,
            ICommunicationFactory communicationFactory,
            IMessage message,
            IQueue queue)
        {
            _plcStateManager = new PlcStateManager();
            _deviceConfigService = deviceConfigService;
            _communicationFactory = communicationFactory;
            _message = message;
            _queue = queue;
        }

        /// <summary>
        /// 内部类管理 PLC 状态
        /// </summary>
        private class PlcStateManager
        {
            public ConcurrentDictionary<string, ICommunication> PlcClients { get; } = new(); // 每个 PLC 一个客户端
            public ConcurrentDictionary<string, bool> PlcConnectionHealth { get; } = new(); // 每个 PLC 一个连接状态
            public ConcurrentDictionary<string, (Task DataTask, CancellationTokenSource DataCts)> DataTasks { get; } = new(); // 每个 PLC 一个数据采集任务
            public ConcurrentDictionary<string, (Task HeartbeatTask, CancellationTokenSource HeartbeatCts)> HeartbeatTasks { get; } = new(); // 每个 PLC 一个心跳检测任务
            public readonly ConcurrentDictionary<string, SemaphoreSlim> PlcLocks = new(); // 每个 PLC 一个锁，用于避免并发问题

            public void Clear()
            {
                PlcClients.Clear();
                PlcConnectionHealth.Clear();
                DataTasks.Clear();
                HeartbeatTasks.Clear();
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
            if (_plcStateManager.DataTasks.ContainsKey(config.Code))
            {
                return;
            }

            _plcStateManager.PlcConnectionHealth[config.Code] = false;

            var cts = new CancellationTokenSource();

            var task = Task.Run(async () =>
            {
                try
                {
                    // 初始化 PLC 客户端和队列管理器
                    var client = CreateCommunicationClient(config);

                    // 启动心跳监控任务（单独管理连接状态）
                    StartHeartbeatMonitor(config);

                    foreach (var module in config.Modules)
                    {
                        _ = Task.Run(async () =>
                        {
                            object prevVal = null;
                            // 循环采集数据，直到取消请求
                            while (!cts.Token.IsCancellationRequested)
                            {
                                // 如果 PLC 已连接则采集数据
                                if (_plcStateManager.PlcConnectionHealth.TryGetValue(config.Code, out var isConnected) && isConnected)
                                {
                                    var trigger = module.Trigger;
                                    object currVal = null;

                                    var locker = _plcStateManager.PlcLocks[config.Code];
                                    await locker.WaitAsync();
                                    try
                                    {
                                        currVal = trigger.Mode == TriggerMode.Always ? null :
                                                await ReadCommunicationValueAsync(client, trigger.Register, trigger.DataType);
                                        if (ShouldSample(trigger.Mode, prevVal, currVal))
                                        {
                                            try
                                            {
                                                var operation = trigger.Operation;
                                                var key = $"{config.Code}:{module.TableName}";
                                                var timestamp = DateTime.Now;
                                                var dataMessage = new DataMessage(timestamp, module.TableName, module.BatchSize, module.DataPoints, operation);
                                                if (operation == DataOperation.Insert)
                                                {
                                                    var batchData = await client.ReadAsync(module.BatchReadRegister, module.BatchReadLength);
                                                    var buffer = batchData.Content;
                                                    foreach (var dataPoint in module.DataPoints)
                                                    {
                                                        var value = TransValue(client, buffer, dataPoint.Index, dataPoint.StringByteLength, dataPoint.DataType, dataPoint.Encoding);
                                                        dataMessage.Values[dataPoint.ColumnName] = value;
                                                    }
                                                    if (!string.IsNullOrEmpty(trigger.TimeColumnName))
                                                    {
                                                        dataMessage.Values[trigger.TimeColumnName] = timestamp;
                                                        _lastStartTimes[key] = timestamp;
                                                        _lastStartTimeColumns[key] = trigger.TimeColumnName;
                                                    }
                                                    _queue.PublishAsync(dataMessage);
                                                }
                                                else if (_lastStartTimes.TryRemove(key, out var startTime))
                                                {
                                                    if (_lastStartTimeColumns.TryRemove(key, out var startColumn))
                                                    {
                                                        dataMessage.KeyValues[startColumn] = startTime;
                                                    }
                                                    if (!string.IsNullOrEmpty(trigger.TimeColumnName))
                                                    {
                                                        dataMessage.Values[trigger.TimeColumnName] = timestamp;
                                                    }
                                                    _queue.PublishAsync(dataMessage);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _ = _message.SendAsync($"[{module.ChamberCode}:{module.TableName}]采集异常: {ex.Message}");
                                            }
                                            prevVal = currVal;
                                        }
                                    }
                                    finally
                                    {
                                        locker.Release();
                                    }
                                }
                            }

                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不做处理
                }
                catch (Exception ex)
                {
                    await _message.SendAsync($"{ex.Message} - StackTrace: {ex.StackTrace}");
                }
            }, cts.Token);

            _plcStateManager.DataTasks.TryAdd(config.Code, (task, cts));
        }

        /// <summary>
        /// 触发模式下是否采样
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="prev"></param>
        /// <param name="curr"></param>
        /// <returns></returns>
        private bool ShouldSample(TriggerMode mode, object prev, object curr)
        {
            if (prev == null) return true;
            decimal p = Convert.ToDecimal(prev);
            decimal c = Convert.ToDecimal(curr);
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
        private void StartHeartbeatMonitor(DeviceConfig config)
        {
            if (_plcStateManager.HeartbeatTasks.ContainsKey(config.Code))
                return;

            var hbCts = new CancellationTokenSource();

            var hbTask = Task.Run(async () =>
            {
                ushort writeData = 0;
                while (!hbCts.IsCancellationRequested)
                {
                    try
                    {
                        var client = _plcStateManager.PlcClients[config.Code];
                        var pingResult = await Task.Run(() => client.IpAddressPing());
                        if (pingResult != IPStatus.Success)
                        {
                            _plcStateManager.PlcConnectionHealth[config.Code] = false;
                            await _message.SendAsync($"网络检测失败：设备 {config.Code}，IP 地址：{config.Host}，故障类型：Ping 未响应");
                            continue;
                        }

                        var connect = await client.WriteUShortAsync(config.HeartbeatMonitorRegister, writeData);
                        if (connect.IsSuccess)
                        {
                            writeData ^= 1;
                            _plcStateManager.PlcConnectionHealth[config.Code] = true;
                            await _message.SendAsync($"心跳正常：设备 {config.Code}");
                        }
                        else
                        {
                            _plcStateManager.PlcConnectionHealth[config.Code] = false;
                            await _message.SendAsync($"通讯故障：设备 {config.Code}，{connect.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _plcStateManager.PlcConnectionHealth[config.Code] = false;
                        await _message.SendAsync(
                            $"系统异常：设备 {config.Code}，异常信息: {ex.Message}，StackTrace: {ex.StackTrace}");
                    }
                    finally
                    {
                        await Task.Delay(config.HeartbeatPollingInterval, hbCts.Token).ConfigureAwait(false);
                    }
                }
            }, hbCts.Token);
            _plcStateManager.HeartbeatTasks.TryAdd(config.Code, (hbTask, hbCts));
        }

        /// <summary>
        /// 读取 PLC 值
        /// </summary>
        /// <param name="plc"></param>
        /// <param name="register"></param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private async Task<object> ReadCommunicationValueAsync(ICommunication client, string register, string dataType)
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


        private dynamic? TransValue(ICommunication client, byte[] buffer, int index, int length, string dataType,
            string encoding)
        {
            switch (dataType.ToLower())
            {
                case "ushort": return client.TransUShort(buffer, index);
                case "uint": return client.TransUInt(buffer, index);
                case "ulong": return client.TransULong(buffer, index);
                case "short": return client.TransShort(buffer, index);
                case "int": return client.TransInt(buffer, index);
                case "long": return client.TransLong(buffer, index);
                case "float": return client.TransFloat(buffer, index);
                case "double": return client.TransDouble(buffer, index);
                case "string": return client.TransString(buffer, index, length, Encoding.GetEncoding(encoding));
                case "bool": return client.TransBool(buffer, index);
                default: return null;
            }
        }

        /// <summary>
        /// 停止所有数据采集任务并释放相关资源
        /// </summary>
        public async Task StopCollectionTasks()
        {
            // 取消数据采集任务
            foreach (var kvp in _plcStateManager.DataTasks)
            {
                await kvp.Value.DataCts.CancelAsync();
            }

            try
            {
                await Task.WhenAll(_plcStateManager.DataTasks.Values.Select(x => x.DataTask));
            }
            catch
            {
                // 忽略取消异常
            }

            // 取消心跳监控任务
            foreach (var kvp in _plcStateManager.HeartbeatTasks)
            {
                await kvp.Value.HeartbeatCts.CancelAsync();
            }

            try
            {
                await Task.WhenAll(_plcStateManager.HeartbeatTasks.Values.Select(x => x.HeartbeatTask));
            }
            catch
            {
                // 忽略取消异常
            }


            // 关闭并清理所有 PLC 客户端
            foreach (var client in _plcStateManager.PlcClients.Values)
            {
                await client.ConnectCloseAsync();
            }

            // 完成并清理队列
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
        /// <returns>写入结果</returns>
        public async Task<CommunicationWriteResult> WritePlcAsync(string plcCode, string address, object value, string dataType)
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
            await locker.WaitAsync();
            try
            {
                switch (dataType)
                {
                    case "ushort":
                        return await client.WriteUShortAsync(address, Convert.ToUInt16(value));
                    case "uint":
                        return await client.WriteUIntAsync(address, Convert.ToUInt32(value));
                    case "ulong":
                        return await client.WriteULongAsync(address, Convert.ToUInt64(value));
                    case "short":
                        return await client.WriteShortAsync(address, Convert.ToInt16(value));
                    case "int":
                        return await client.WriteIntAsync(address, Convert.ToInt32(value));
                    case "long":
                        return await client.WriteLongAsync(address, Convert.ToInt64(value));
                    case "float":
                        return await client.WriteFloatAsync(address, Convert.ToSingle(value));
                    case "double":
                        return await client.WriteDoubleAsync(address, Convert.ToDouble(value));
                    case "string":
                        return await client.WriteStringAsync(address, Convert.ToString(value) ?? string.Empty);
                    case "bool":
                        return await client.WriteBoolAsync(address, Convert.ToBoolean(value));
                    default:
                        return new CommunicationWriteResult
                        {
                            IsSuccess = false,
                            Message = $"不支持的数据类型: {dataType}"
                        };
                }
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
