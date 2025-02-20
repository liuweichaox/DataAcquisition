using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Services.DataAcquisitionConfigs;
using DataAcquisition.Services.Messages;
using DataAcquisition.Services.QueueManagers;

namespace DataAcquisition.Services.DataAcquisitions
{
    /// <summary>
    /// 数据采集器
    /// </summary>
    public class DataAcquisitionService(
        IDataAcquisitionConfigService dataAcquisitionConfigService,
        IPlcClientFactory plcClientFactory,
        IDataStorageFactory dataStorageFactory,
        IQueueManagerFactory queueManagerFactory,
        IMessageService messageService)
        : IDataAcquisitionService
    {
        /// <summary>
        /// PLC 客户端管理
        /// </summary>
        private readonly ConcurrentDictionary<string, IPlcClient> _plcClients = new();

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
                StartCollectionTask(config);
            }
        }

        /// <summary>
        /// 启动单个采集任务（如果任务已存在则直接返回）
        /// </summary>
        private void StartCollectionTask(DataAcquisitionConfig config)
        {
            if (_dataTasks.ContainsKey(config.Id))
            {
                return;
            }

            var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";
            _plcConnectionStatus[plcKey] = false;

            var cts = new CancellationTokenSource();

            var task = Task.Run(async () =>
            {
                try
                {
                    // 初始化 PLC 客户端和队列管理器
                    await CreatePlcClientAsync(config);
                    InitializeQueueManager(config);

                    // 启动心跳监控任务（单独管理连接状态）
                    StartHeartbeatMonitor(config);

                    // 循环采集数据，直到取消请求
                    while (!cts.Token.IsCancellationRequested)
                    {
                        // 如果 PLC 已连接则采集数据
                        if (_plcConnectionStatus.TryGetValue(plcKey, out var isConnected) && isConnected)
                        {
                            await DataCollectAsync(config);
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
                    await messageService.SendAsync(
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {ex.Message} - StackTrace: {ex.StackTrace}");
                }
            }, cts.Token);

            _dataTasks.TryAdd(config.Id, (task, cts));
        }

        /// <summary>
        /// 创建 PLC 客户端（若已存在则直接返回）
        /// </summary>
        private async Task CreatePlcClientAsync(DataAcquisitionConfig config)
        {
            var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";

            if (_plcClients.ContainsKey(plcKey))
            {
                return;
            }
            
            var plcClient = plcClientFactory.Create(config);
            _plcClients.TryAdd(plcKey, plcClient);

            var connect = await plcClient.ConnectServerAsync();
            if (connect.IsSuccess)
            {
                _plcConnectionStatus[plcKey] = true;
                await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接 {plcKey} 成功");
            }
            else
            {
                _plcConnectionStatus[plcKey] = false;
                await messageService.SendAsync(
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接 {plcKey} 失败: {connect.Message}");
            }
        }

        /// <summary>
        /// 初始化队列管理器（数据入库、缓冲处理等）
        /// </summary>
        private void InitializeQueueManager(DataAcquisitionConfig config)
        {
            var dataStorage = dataStorageFactory.Create(config);
            _queueManagers.GetOrAdd(config.Id, _ => queueManagerFactory.Create(dataStorage, config, messageService));
        }

        /// <summary>
        /// 检查 PLC 连接状态，若断开则尝试重连
        /// </summary>
        private void StartHeartbeatMonitor(DataAcquisitionConfig config)
        {
            var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";
            if (_heartbeatTasks.ContainsKey(plcKey))
                return;

            var hbCts = new CancellationTokenSource();
            var hbTask = Task.Run(async () =>
            {
                while (!hbCts.IsCancellationRequested)
                {
                    try
                    {
                        if (_plcConnectionStatus.TryGetValue(plcKey, out var isConnected) && isConnected)
                        {
                            continue;
                        }

                        var plcClient = _plcClients[plcKey];
                        var pingResult = await plcClient.IpAddressPingAsync();
                        if (!pingResult.IsSuccess)
                        {
                            _plcConnectionStatus[plcKey] = false;
                            await messageService.SendAsync(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{plcKey} 设备不可达");
                            continue;
                        }

                        var connect = await plcClient.ConnectServerAsync();
                        if (connect.IsSuccess)
                        {
                            _plcConnectionStatus[plcKey] = true;
                            await messageService.SendAsync(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{plcKey} 恢复连接");
                        }
                        else
                        {
                            _plcConnectionStatus[plcKey] = false;
                            await messageService.SendAsync(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{plcKey} 连接失败，等待下次检测...");
                        }
                    }
                    catch (Exception ex)
                    {
                        _plcConnectionStatus[plcKey] = false;
                        await messageService.SendAsync(
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{plcKey} 连接异常: {ex.Message} - StackTrace: {ex.StackTrace}");
                    }
                    finally
                    {
                        await Task.Delay(config.HeartbeatIntervalMs, hbCts.Token).ConfigureAwait(false);
                    }
                }
            }, hbCts.Token);
            _heartbeatTasks.TryAdd(plcKey, (hbTask, hbCts));
        }

        /// <summary>
        /// 数据采集与异常处理
        /// </summary>
        private async Task DataCollectAsync(DataAcquisitionConfig config)
        {
            try
            {
                var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";
                var plcClient = _plcClients[plcKey];

                var operationResult = await plcClient.ReadAsync(config.Plc.RegisterByteAddress, config.Plc.RegisterByteLength);
                if (!operationResult.IsSuccess)
                {
                    await messageService.SendAsync(
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取失败：{config.Plc.Code} 地址：{config.Plc.RegisterByteAddress}, 长度: {operationResult.Message}");
                   return;
                }
                
                var buffer = operationResult.Content;
                
                foreach (var registerGroup in config.Plc.RegisterGroups)
                {
                    var data = new Dictionary<string, object>();
                    foreach (var register in registerGroup.Registers)
                    {
                        try
                        {
                            var value =  ParseValue(plcClient, buffer, register.Index, register.ByteLength, register.DataType, register.Encoding);
                            data[register.ColumnName] = value;
                        }
                        catch (Exception ex)
                        {
                            await messageService.SendAsync(
                                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取异常：{config.Plc.Code} 地址：{config.Plc.IpAddress} 索引位置：{register.Index}: {ex.Message} - StackTrace: {ex.StackTrace}");
                        }
                    }

                    if (data.Count > 0)
                    {
                        var dataPoint = new DataPoint(registerGroup.TableName,   data);
                        if (_queueManagers.TryGetValue(config.Id, out var queueManager))
                        {
                            queueManager.EnqueueData(dataPoint);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await messageService.SendAsync(
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {ex.Message} - StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 根据数据类型映射对应的读取操作
        /// </summary>
        private object ParseValue(IPlcClient plcClient, byte[] buffer, int index, int length, string dataType, string encoding)
        {
            var operations = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ushort"] =  () =>  plcClient.TransUInt16(buffer, length),
                ["uint"] =  () => plcClient.TransUInt32(buffer, length),
                ["ulong"] =  () => plcClient.TransUInt64(buffer, length),
                ["short"] =  () => plcClient.TransInt16(buffer, length),
                ["int"] =  () => plcClient.TransInt32(buffer, length),
                ["long"] =  () => plcClient.TransInt64(buffer, length),
                ["float"] =  () => plcClient.TransSingle(buffer, length),
                ["double"] =  () => plcClient.TransDouble(buffer, length),
                ["string"] =  () => plcClient.TransString(buffer, index, length, encoding),
                ["bool"] =  () => plcClient.TransBool(buffer, length),
            };

            if (operations.TryGetValue(dataType, out var operation))
            {
                return operation();
            }
            else
            {
                throw new ArgumentException($"不支持的数据类型: {dataType}");
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