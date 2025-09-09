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
        private readonly ConcurrentDictionary<string, Dictionary<string, object>> _lastBatchKeys = new();

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
            public readonly ConcurrentDictionary<string, object> PlcLocks = new(); // 每个 PLC 一个锁，用于避免并发问题

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
                        _ = Task.Run(() =>
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

                                    lock (_plcStateManager.PlcLocks[config.Code])
                                    {
                                        currVal = trigger.Mode == TriggerMode.Always ? null :
                                                ReadCommunicationValue(client, trigger.Register, trigger.DataType);
                                        if (ShouldSample(trigger.Mode, prevVal, currVal))
                                        {
                                            try
                                            {
                                                var operation = module.Operation;
                                                var key = $"{config.Code}:{module.TableName}";
                                                var dataMessage = new DataMessage(DateTime.Now, module.TableName, module.BatchSize, module.DataPoints, operation);
                                                if (operation == DataOperation.Insert)
                                                {
                                                    var batchData = client.Read(module.BatchReadRegister, module.BatchReadLength);
                                                    var buffer = batchData.Content;
                                                    var keyValues = new Dictionary<string, object>();
                                                    foreach (var dataPoint in module.DataPoints)
                                                    {
                                                        var value = TransValue(client, buffer, dataPoint.Index, dataPoint.StringByteLength, dataPoint.DataType, dataPoint.Encoding);
                                                        dataMessage.Values[dataPoint.ColumnName] = value;
                                                        keyValues[dataPoint.ColumnName] = value;
                                                    }
                                                    dataMessage.Values["StartTime"] = DateTime.Now;
                                                    _lastBatchKeys[key] = keyValues;
                                                    _queue.PublishAsync(dataMessage);
                                                }
                                                else if (_lastBatchKeys.TryRemove(key, out var keyValues))
                                                {
                                                    foreach (var kvp in keyValues)
                                                    {
                                                        dataMessage.KeyValues[kvp.Key] = kvp.Value;
                                                    }
                                                    dataMessage.Values["EndTime"] = DateTime.Now;
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
            _plcStateManager.PlcLocks.TryAdd(config.Code, new object());
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
                var writeData = 0;
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

                        var connect = await client.WriteAsync(config.HeartbeatMonitorRegister, writeData);
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
        private object ReadCommunicationValue(ICommunication client, string register, string dataType)
        {
            return dataType switch
            {
                "ushort" => client.ReadUInt16(register),
                "uint" => client.ReadUInt32(register),
                "ulong" => client.ReadUInt64(register),
                "short" => client.ReadInt16(register),
                "int" => client.ReadInt32(register),
                "long" => client.ReadInt64(register),
                "float" => client.ReadFloat(register),
                "double" => client.ReadDouble(register),
                _ => throw new NotSupportedException($"不支持的数据类型: {dataType}")
            };
        }


        private dynamic? TransValue(ICommunication client, byte[] buffer, int index, int length, string dataType,
            string encoding)
        {
            switch (dataType.ToLower())
            {
                case "ushort": return client.TransUInt16(buffer, index);
                case "uint": return client.TransUInt32(buffer, index);
                case "ulong": return client.TransUInt64(buffer, index);
                case "short": return client.TransInt16(buffer, index);
                case "int": return client.TransInt32(buffer, index);
                case "long": return client.TransInt64(buffer, index);
                case "float": return client.TransSingle(buffer, index);
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
