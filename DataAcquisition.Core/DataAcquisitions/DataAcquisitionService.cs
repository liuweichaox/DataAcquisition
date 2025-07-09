using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Core.Communication;
using DataAcquisition.Core.DeviceConfigs;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.QueueManagers;
using HarmonyLib;
using HslCommunication.Core.Device;

namespace DataAcquisition.Core.DataAcquisitions
{

    /// <summary>
    /// 数据采集器
    /// </summary>
    public class DataAcquisitionService : IDataAcquisitionService
    {
        private readonly PlcStateManager _plcStateManager;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly IPlcDriverFactory _plcDriverFactory;
        private readonly IQueueManagerFactory _queueManagerFactory;
        private readonly IMessageService _messageService;

        /// <summary>
        /// 数据采集器
        /// </summary>
        public DataAcquisitionService(IDeviceConfigService deviceConfigService,
            IPlcDriverFactory plcDriverFactory,
            IQueueManagerFactory queueManagerFactory,
            IMessageService messageService)
        {
            _plcStateManager = new PlcStateManager();
            _deviceConfigService = deviceConfigService;
            _plcDriverFactory = plcDriverFactory;
            _queueManagerFactory = queueManagerFactory;
            _messageService = messageService;

            // 获取目标方法
            var harmony = new Harmony("simple.hsl.bypass");
            var type = Type.GetType("HslCommunication.Authorization, HslCommunication");
            var method = type?.GetMethod("nzugaydgwadawdibbas", BindingFlags.NonPublic | BindingFlags.Static);
            // 注入 Patch
            harmony.Patch(method, prefix: new HarmonyMethod(ByPassAuth));
            // 测试验证
            var result = (bool?)method?.Invoke(null, null);
            Console.WriteLine($"授权是否成功：{result}");  // 应该输出 true
        }

        /// <summary>
        /// 绕过验证
        /// </summary>
        /// <param name="__result"></param>
        /// <returns></returns>
        private static bool ByPassAuth(ref bool __result)
        {
            __result = true;
            return false;  // 跳过原方法
        }

        /// <summary>
        /// 内部类管理 PLC 状态
        /// </summary>
        private class PlcStateManager
        {
            public ConcurrentDictionary<string, DeviceTcpNet> PlcClients { get; } = new(); // 每个 PLC 一个客户端
            public ConcurrentDictionary<string, IQueueManager> QueueManagers { get; } = new(); // 每个 PLC 一个消息都队列
            public ConcurrentDictionary<string, bool> PlcConnectionHealth { get; } = new(); // 每个 PLC 一个连接状态
            public ConcurrentDictionary<string, (Task DataTask, CancellationTokenSource DataCts)> DataTasks { get; } = new(); // 每个 PLC 一个数据采集任务
            public ConcurrentDictionary<string, (Task HeartbeatTask, CancellationTokenSource HeartbeatCts)> HeartbeatTasks { get; } = new(); // 每个 PLC 一个心跳检测任务
            public ConcurrentDictionary<string, Dictionary<string, Chamber>> DeviceChamberStatus { get; } = new(); // 每个 PLC 的腔室状态

            public void Clear()
            {
                PlcClients.Clear();
                QueueManagers.Clear();
                PlcConnectionHealth.Clear();
                DataTasks.Clear();
                HeartbeatTasks.Clear();
                DeviceChamberStatus.Clear();
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
                    var plcClient = CreatePlcClient(config);

                    var queueManager = _plcStateManager.QueueManagers.GetOrAdd(config.Code, _ => _queueManagerFactory.Create(config));

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
                                    bool shouldSample = false;
                                    var trigger = module.Trigger;
                                    object currVal = trigger.Mode == TriggerMode.Always ? null :
                                            ReadPlcValue(plcClient, trigger.Register, trigger.DataType);
                                    if (ShouldSample(trigger.Mode, prevVal, currVal))
                                    {
                                        var batchData = plcClient.Read(module.BatchReadRegister, module.BatchReadLength);
                                        var buffer = batchData.Content;
                                        var dataMessage = new DataMessage(DateTime.Now, module.TableName, module.DataPoints);
                                        foreach (var dataPoint in module.DataPoints)
                                        {
                                            var value = TransValue(plcClient, buffer, dataPoint.Index, dataPoint.StringByteLength, dataPoint.DataType, dataPoint.Encoding);
                                            dataMessage.Values[dataPoint.ColumnName] = value;
                                        }
                                        queueManager.EnqueueData(dataMessage);
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
                    await _messageService.SendAsync($"{ex.Message} - StackTrace: {ex.StackTrace}");
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
        private DeviceTcpNet CreatePlcClient(DeviceConfig config)
        {
            if (_plcStateManager.PlcClients.TryGetValue(config.Code, out var plcClient))
            {
                return plcClient;
            }

            plcClient = _plcDriverFactory.Create(config);
            _plcStateManager.PlcClients.TryAdd(config.Code, plcClient);

            return plcClient;
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
                        var plcClient = _plcStateManager.PlcClients[config.Code];
                        var pingResult = await Task.Run(() => plcClient.IpAddressPing());
                        if (pingResult != IPStatus.Success)
                        {
                            _plcStateManager.PlcConnectionHealth[config.Code] = false;
                            await _messageService.SendAsync($"网络检测失败：设备 {config.Code}，IP 地址：{config.Host}，故障类型：Ping 未响应");
                            continue;
                        }

                        var connect = await plcClient.WriteAsync(config.HeartbeatMonitorRegister, writeData);
                        if (connect.IsSuccess)
                        {
                            writeData ^= 1;
                            _plcStateManager.PlcConnectionHealth[config.Code] = true;
                            await _messageService.SendAsync($"心跳正常：设备 {config.Code}");
                        }
                        else
                        {
                            _plcStateManager.PlcConnectionHealth[config.Code] = false;
                            await _messageService.SendAsync($"通讯故障：设备 {config.Code}，{connect.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _plcStateManager.PlcConnectionHealth[config.Code] = false;
                        await _messageService.SendAsync(
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
        /// 计算地址
        /// </summary>
        /// <param name="baseAddress"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private string GetAddress(string baseAddress, int index)
        {
            // 提取前缀字母和数字部分
            string prefix = new string(baseAddress.TakeWhile(char.IsLetter).ToArray());
            string numberPart = new string(baseAddress.SkipWhile(char.IsLetter).ToArray());

            if (int.TryParse(numberPart, out int baseNumber))
            {
                // 计算新地址：每两个寄存器是一个单位
                int newAddressNumber = baseNumber + index / 2;
                return prefix + newAddressNumber;
            }

            throw new ArgumentException("地址格式错误：" + baseAddress);
        }

        /// <summary>
        /// 读取 PLC 值
        /// </summary>
        /// <param name="plc"></param>
        /// <param name="register"></param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private object ReadPlcValue(DeviceTcpNet plc, string register, string dataType)
        {
            return dataType switch
            {
                "ushort" => plc.ReadUInt16(register, 1),
                "uint" => plc.ReadUInt32(register, 1),
                "ulong" => plc.ReadUInt64(register, 1),
                "short" => plc.ReadInt16(register, 1),
                "int" => plc.ReadInt32(register, 1),
                "long" => plc.ReadInt64(register, 1),
                "float" => plc.ReadFloat(register, 1),
                "double" => plc.ReadDouble(register, 1),
                _ => throw new NotSupportedException($"不支持的数据类型: {dataType}")
            };
        }


        private dynamic? TransValue(DeviceTcpNet plcClient, byte[] buffer, int index, int length, string dataType,
            string encoding)
        {
            switch (dataType.ToLower())
            {
                case "ushort": return plcClient.ByteTransform.TransUInt16(buffer, index);
                case "uint": return plcClient.ByteTransform.TransUInt32(buffer, index);
                case "ulong": return plcClient.ByteTransform.TransUInt64(buffer, index);
                case "short": return plcClient.ByteTransform.TransInt16(buffer, index);
                case "int": return plcClient.ByteTransform.TransInt32(buffer, index);
                case "long": return plcClient.ByteTransform.TransInt64(buffer, index);
                case "float": return plcClient.ByteTransform.TransSingle(buffer, index);
                case "double": return plcClient.ByteTransform.TransDouble(buffer, index);
                case "string": return plcClient.ByteTransform.TransString(buffer, index, length, Encoding.GetEncoding(encoding));
                case "bool": return plcClient.ByteTransform.TransBool(buffer, index);
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
            foreach (var plcClient in _plcStateManager.PlcClients.Values)
            {
                await plcClient.ConnectCloseAsync();
            }

            // 完成并清理队列管理器
            foreach (var queueManager in _plcStateManager.QueueManagers.Values)
            {
                queueManager.Dispose();
            }

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