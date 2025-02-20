using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Services.DataAcquisitionConfigs;
using DataAcquisition.Services.Messages;
using DataAcquisition.Services.QueueManagers;
using NCalc;

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
        /// 信号量管理
        /// </summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionLocks = new();
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
        private readonly ConcurrentDictionary<string, (Task DataTask, CancellationTokenSource DataCts)> _dataTasks = new();
        /// <summary>
        /// 心跳检测任务和取消令牌管理
        /// </summary>
        private readonly ConcurrentDictionary<string, (Task HeartbeatTask, CancellationTokenSource HeartbeatCts)> _heartbeatTasks = new();

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
                        
                        await Task.Delay(config.CollectIntervaMs, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不做处理
                }
                catch (Exception ex)
                {
                    await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {ex.Message} - StackTrace: {ex.StackTrace}");
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

            // 获取或创建对应的信号量，确保同一 PLC 不会并发连接
            var semaphore = _connectionLocks.GetOrAdd(plcKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                // 双重检查:若在等待期间已有客户端创建则直接返回
                if (!_plcClients.ContainsKey(plcKey))
                {
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
                        await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接 {plcKey} 失败: {connect.Message}");
                    }
                }
            }
            finally
            {
                semaphore.Release();
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
                            await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{plcKey} 设备不可达");
                            continue;
                        }

                        var connect = await plcClient.ConnectServerAsync();
                        if (connect.IsSuccess)
                        {
                            _plcConnectionStatus[plcKey] = true;
                            await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{plcKey} 恢复连接");
                        }
                        else
                        {
                            _plcConnectionStatus[plcKey] = false;
                            await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{plcKey} 连接失败，等待下次检测...");
                        }
                    }
                    catch (Exception ex)
                    {
                        _plcConnectionStatus[plcKey] = false;
                        await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 心跳检测：{plcKey} 连接异常: {ex.Message} - StackTrace: {ex.StackTrace}");
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
                var data = new Dictionary<string, object>();
                
                foreach (var register in config.Plc.Registers)
                {
                    try
                    {
                        var result = await ParseValue(plcClient, register.DataAddress, register.DataLength, register.DataType);
                        if (result.IsSuccess)
                        {
                            data[register.ColumnName] = await ContentHandle(register, result.Content);
                        }
                        else
                        {
                            await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取失败：{config.Plc.Code} 地址：{register.DataAddress}: {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取异常：{config.Plc.Code} 地址：{register.DataAddress}: {ex.Message} - StackTrace: {ex.StackTrace}");
                    }
                }

                if (data.Count > 0)
                {
                    var dataPoint = new DataPoint(data);
                    if (_queueManagers.TryGetValue(config.Id, out var queueManager))
                    {
                        queueManager.EnqueueData(dataPoint);
                    }
                }
            }
            catch (Exception ex)
            {
                await messageService.SendAsync($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {ex.Message} - StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 对读取到的数据进行表达式计算处理
        /// </summary>
        private static async Task<object> ContentHandle(Register register, object content)
        {
            if (string.IsNullOrWhiteSpace(register.EvalExpression))
            {
                return content;
            }

            var types = new[] { "ushort","uint", "ulong", "int", "long", "float", "double" };
            if (!types.Contains(register.DataType))
            {
                return content;
            }
            
            var expression = new AsyncExpression(register.EvalExpression)
            {
                Parameters =
                {
                    ["value"] = content
                }
            };
            
            var value = await expression.EvaluateAsync();
            return value ?? 0;
        }

        /// <summary>
        /// 根据数据类型映射对应的读取操作
        /// </summary>
        private async Task<OperationResult> ParseValue(IPlcClient plcClient, string dataAddress, ushort dataLength, string dataType)
        {
            var operations = new Dictionary<string, Func<Task<OperationResult>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ushort"] = async () => OperationResult.From(await plcClient.ReadUInt16Async(dataAddress)),
                ["uint"]   = async () => OperationResult.From(await plcClient.ReadUInt32Async(dataAddress)),
                ["ulong"]  = async () => OperationResult.From(await plcClient.ReadUInt64Async(dataAddress)),
                ["short"]  = async () => OperationResult.From(await plcClient.ReadInt16Async(dataAddress)),
                ["int"]    = async () => OperationResult.From(await plcClient.ReadInt32Async(dataAddress)),
                ["long"]   = async () => OperationResult.From(await plcClient.ReadInt64Async(dataAddress)),
                ["float"]  = async () => OperationResult.From(await plcClient.ReadFloatAsync(dataAddress)),
                ["double"] = async () => OperationResult.From(await plcClient.ReadDoubleAsync(dataAddress)),
                ["string"] = async () => OperationResult.From(await plcClient.ReadStringAsync(dataAddress, dataLength)),
                ["bool"]   = async () => OperationResult.From(await plcClient.ReadBoolAsync(dataAddress))
            };

            if (operations.TryGetValue(dataType, out var operation))
            {
                return await operation();
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

            // 释放信号量
            foreach (var semaphore in _connectionLocks.Values)
            {
                semaphore.Dispose();
            }
            _connectionLocks.Clear();

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
