﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Core.Communication;
using DataAcquisition.Core.DataAcquisitionConfigs;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Delegates;
using DataAcquisition.Core.QueueManagers;

namespace DataAcquisition.Core.DataAcquisitions
{
    /// <summary>
    /// 数据采集器
    /// </summary>
    public class DataAcquisitionService(
        IDataAcquisitionConfigService dataAcquisitionConfigService,
        IPlcDriverFactory plcDriverFactory,
        IDataStorageFactory dataStorageFactory,
        IQueueManagerFactory queueManagerFactory,
        MessageSendDelegate messageSendDelegate)
        : IDataAcquisitionService
    {
        /// <summary>
        /// PLC 客户端管理
        /// </summary>
        private readonly ConcurrentDictionary<string, IPlcDriver> _plcClients = new();

        /// <summary>
        /// PLC 连接状态管理
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> _plcConnectionStatus = new();

        /// <summary>
        /// 消息队列管理
        /// </summary>
        private readonly ConcurrentDictionary<string, IQueueManager> _queueManagers = new();

        /// <summary>
        /// 数据采集任务和取消令牌管理
        /// </summary>
        private readonly ConcurrentDictionary<string, (Task DataTask, CancellationTokenSource DataCts)> _dataTasks =
            new();

        /// <summary>
        /// 心跳检测任务和取消令牌管理
        /// </summary>
        private readonly ConcurrentDictionary<string, (Task HeartbeatTask, CancellationTokenSource HeartbeatCts)>
            _heartbeatTasks = new();

        /// <summary>
        /// 开始所有采集任务
        /// </summary>
        public async Task StartCollectionTasks()
        {
            var dataAcquisitionConfigs = await dataAcquisitionConfigService.GetConfigs();
            foreach (var config in dataAcquisitionConfigs)
            {
                if (!config.IsEnabled) continue;
                Work(config);
            }
        }

        /// <summary>
        /// 启动单个采集任务（如果任务已存在则直接返回）
        /// </summary>
        private void Work(DataAcquisitionConfig config)
        {
            if (_dataTasks.ContainsKey(config.Plc.Code))
            {
                return;
            }
            
            _plcConnectionStatus[config.Plc.Code] = false;

            var cts = new CancellationTokenSource();

            var task = Task.Run(async () =>
            {
                try
                {
                    // 初始化 PLC 客户端和队列管理器
                    var plcClient = await CreatePlcClientAsync(config);
                    var queueManager = CreateQueueManager(config);

                    // 启动心跳监控任务（单独管理连接状态）
                    StartHeartbeatMonitor(config);

                    // 循环采集数据，直到取消请求
                    while (!cts.Token.IsCancellationRequested)
                    {
                        // 如果 PLC 已连接则采集数据
                        if (_plcConnectionStatus.TryGetValue(config.Plc.Code, out var isConnected) && isConnected)
                        {
                            DataCollect(config, plcClient, queueManager);
                        }

                        await Task.Delay(config.CollectIntervalMs, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不做处理
                }
                catch (Exception ex)
                {
                    await messageSendDelegate(
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {ex.Message} - StackTrace: {ex.StackTrace}");
                }
            }, cts.Token);

            _dataTasks.TryAdd(config.Plc.Code, (task, cts));
        }

        /// <summary>
        /// 创建 PLC 客户端（若已存在则直接返回）
        /// </summary>
        private async Task<IPlcDriver> CreatePlcClientAsync(DataAcquisitionConfig config)
        {
            if (_plcClients.TryGetValue(config.Plc.Code, out var plcClient))
            {
                return plcClient;
            }

            plcClient = plcDriverFactory.Create(config);
            _plcClients.TryAdd(config.Plc.Code, plcClient);

            var connect = await plcClient.ConnectServerAsync();
            if (connect.IsSuccess)
            {
                _plcConnectionStatus[config.Plc.Code] = true;
                await messageSendDelegate($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接 {config.Plc.Code} 成功");
            }
            else
            {
                _plcConnectionStatus[config.Plc.Code] = false;
                await messageSendDelegate(
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接 {config.Plc.Code} 失败: {connect.Message}");
            }

            return plcClient;
        }

        /// <summary>
        /// 创建队列管理器（数据入库、缓冲处理等）
        /// </summary>
        private IQueueManager CreateQueueManager(DataAcquisitionConfig config)
        {
            var dataStorage = dataStorageFactory.Create(config);
            return _queueManagers.GetOrAdd(config.Plc.Code, _ => queueManagerFactory.Create(dataStorage, config, messageSendDelegate));
        }

        /// <summary>
        /// 检查 PLC 连接状态，若断开则尝试重连
        /// </summary>
        private void StartHeartbeatMonitor(DataAcquisitionConfig config)
        {
            
            if (_heartbeatTasks.ContainsKey(config.Plc.Code))
                return;

            var hbCts = new CancellationTokenSource();
            var hbTask = Task.Run(async () =>
            {
                while (!hbCts.IsCancellationRequested)
                {
                    try
                    {
                        var plcClient = _plcClients[config.Plc.Code];
                        var pingResult = await plcClient.IpAddressPingAsync();
                        if (!pingResult.IsSuccess)
                        {
                            _plcConnectionStatus[config.Plc.Code] = false;
                            await messageSendDelegate(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{config.Plc.Code} 设备不可达");
                            continue;
                        }

                        var connect = await plcClient.ConnectServerAsync();
                        if (connect.IsSuccess)
                        {
                            _plcConnectionStatus[config.Plc.Code] = true;
                            await messageSendDelegate(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{config.Plc.Code} 正常");
                        }
                        else
                        {
                            _plcConnectionStatus[config.Plc.Code] = false;
                            await messageSendDelegate(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{config.Plc.Code} 连接失败，等待下次检测...");
                        }
                    }
                    catch (Exception ex)
                    {
                        _plcConnectionStatus[config.Plc.Code] = false;
                        await messageSendDelegate(
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{config.Plc.Code} 连接异常: {ex.Message} - StackTrace: {ex.StackTrace}");
                    }
                    finally
                    {
                        await Task.Delay(config.HeartbeatIntervalMs, hbCts.Token).ConfigureAwait(false);
                    }
                }
            }, hbCts.Token);
            _heartbeatTasks.TryAdd(config.Plc.Code, (hbTask, hbCts));
        }

        /// <summary>
        /// 数据采集与异常处理
        /// </summary>
        private void DataCollect(DataAcquisitionConfig config, IPlcDriver plcDriver, IQueueManager queueManager)
        {
            try
            {
                var operationResult = plcDriver.Read(config.Plc.BatchReadAddress, config.Plc.BatchReadLength);
                if (!operationResult.IsSuccess)
                {
                    _ = messageSendDelegate(
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取 {config.Plc.BatchReadAddress} 失败：{config.Plc.Code}");
                }

                var buffer = operationResult.Content;
                var timestamp = DateTime.Now;
                if (buffer.Length == 0)
                {
                    return;
                }

                foreach (var registerGroup in config.Plc.RegisterGroups)
                {
                    var dataPoint = new DataPoint(timestamp, registerGroup.TableName);
                    foreach (var register in registerGroup.Registers)
                    {
                        var value = TransValue(plcDriver, buffer, register.Index, register.StringByteLength ?? 0, register.DataType, register.Encoding);
                        dataPoint.Values.TryAdd(register.ColumnName, value);
                    }
                    queueManager.EnqueueData(dataPoint);
                }
            }
            catch (Exception ex)
            {
                _ = messageSendDelegate(
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {ex.Message} - StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 根据数据类型映射对应的读取操作
        /// </summary>
        private dynamic TransValue(IPlcDriver plcDriver, byte[] buffer, int index, int length, string dataType,
            string encoding)
        {
            switch (dataType.ToLower())
            {
                case "ushort": return plcDriver.TransUInt16(buffer, length);
                case "uint": return plcDriver.TransUInt32(buffer, length);
                case "ulong": return plcDriver.TransUInt64(buffer, length);
                case "short": return plcDriver.TransInt16(buffer, length);
                case "int": return plcDriver.TransInt32(buffer, length);
                case "long": return plcDriver.TransInt64(buffer, length);
                case "float": return plcDriver.TransSingle(buffer, length);
                case "double": return plcDriver.TransDouble(buffer, length);
                case "string": return plcDriver.TransString(buffer, index, length, encoding);
                case "bool": return plcDriver.TransBool(buffer, length);
                default: return null;
            }
        }

        /// <summary>
        /// 停止所有数据采集任务并释放相关资源
        /// </summary>
        public async Task StopCollectionTasks()
        {
            // 取消数据采集任务
            foreach (var kvp in _dataTasks)
            {
                kvp.Value.DataCts.Cancel();
            }

            try
            {
                await Task.WhenAll(_dataTasks.Values.Select(x => x.DataTask));
            }
            catch
            {
                // 忽略取消异常
            }

            _dataTasks.Clear();

            // 取消心跳监控任务
            foreach (var kvp in _heartbeatTasks)
            {
                kvp.Value.HeartbeatCts.Cancel();
            }

            try
            {
                await Task.WhenAll(_heartbeatTasks.Values.Select(x => x.HeartbeatTask));
            }
            catch
            {
                // 忽略取消异常
            }

            _heartbeatTasks.Clear();

            // 关闭并清理所有 PLC 客户端
            foreach (var plcClient in _plcClients.Values)
            {
                await plcClient.ConnectCloseAsync();
            }

            _plcClients.Clear();
            _plcConnectionStatus.Clear();

            // 完成并清理队列管理器
            foreach (var queueManager in _queueManagers.Values)
            {
                queueManager.Complete();
            }

            _queueManagers.Clear();
        }

        /// <summary>
        /// 获取当前所有 PLC 连接状态
        /// </summary>
        public SortedDictionary<string, bool> GetPlcConnectionStatus()
        {
            return new SortedDictionary<string, bool>(_plcConnectionStatus);
        }
    }
}