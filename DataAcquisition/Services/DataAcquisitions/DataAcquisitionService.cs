using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Common;
using DataAcquisition.Services.DataAcquisitionConfigs;
using DataAcquisition.Services.QueueManagers;
using NCalc;

namespace DataAcquisition.Services.DataAcquisitions
{
    /// <summary>
    /// 数据采集器
    /// </summary>
    public class DataAcquisitionService(
        IDataAcquisitionConfigService dataAcquisitionConfigService,
        PlcClientFactory plcClientFactory,
        DataStorageFactory dataStorageFactory,
        QueueManagerFactory queueManagerFactory,
        ProcessDataPoint processDataPoint,
        MessageHandle messageHandle)
        : IDataAcquisitionService
    {
        // 用于控制同一 PLC 客户端的并发连接操作
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        // 存储 PLC 客户端
        private readonly ConcurrentDictionary<string, IPlcClient> _plcClients = new();

        // 保存 PLC 连接状态（true 表示已连接）
        private readonly ConcurrentDictionary<string, bool> _plcConnectionStatus = new();

        // 存储队列管理器（数据入库、处理等）
        private readonly ConcurrentDictionary<int, IQueueManager> _queueManagers = new();

        // 存储采集任务及其取消令牌（以 config.Id 为 key）
        private readonly ConcurrentDictionary<int, (Task Task, CancellationTokenSource Cts)> _runningTasks = new();

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
            if (_runningTasks.ContainsKey(config.Id))
            {
                return;
            }

            var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";
            _plcConnectionStatus[plcKey] = false;

            var cts = new CancellationTokenSource();
            var token = cts.Token;

            var task = Task.Run(async () =>
            {
                try
                {
                    // 初始化 PLC 客户端和队列管理器
                    await CreatePlcClientAsync(config);
                    InitializeQueueManager(config);

                    // 循环采集数据，直到取消请求
                    while (!token.IsCancellationRequested)
                    {
                        await DataCollectAsync(config);
                        await Task.Delay(config.CollectionFrequency, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不做处理
                }
                catch (Exception ex)
                {
                    messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - Task for Config {config.Id} encountered an error: {ex.Message}");
                }
            }, token);

            _runningTasks.TryAdd(config.Id, (task, cts));
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
            var semaphore = _locks.GetOrAdd(plcKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                // 双重检查：若在等待期间已有客户端创建则直接返回
                if (!_plcClients.ContainsKey(plcKey))
                {
                    var plcClient = plcClientFactory(config.Plc.IpAddress, config.Plc.Port);
                    _plcClients.TryAdd(plcKey, plcClient);

                    var connect = await plcClient.ConnectServerAsync();
                    if (connect.IsSuccess)
                    {
                        _plcConnectionStatus[plcKey] = true;
                        messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 成功连接到设备 {plcKey}！");
                    }
                    else
                    {
                        _plcConnectionStatus[plcKey] = false;
                        messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接设备 {plcKey} 失败：{connect.Message}");
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
            _queueManagers.GetOrAdd(config.Id, _ => queueManagerFactory(dataStorageFactory, config));
        }

        /// <summary>
        /// 数据采集与异常处理
        /// </summary>
        private async Task DataCollectAsync(DataAcquisitionConfig config)
        {
            try
            {
                // 检查 PLC 连接状态，如断开则尝试重连
                await IfPlcClientNotConnectedReconnectAsync(config);

                // 读取数据（可以对单个寄存器读取失败进行容错处理）
                var dataPoint = await ReadAsync(config);
                if (dataPoint != null && _queueManagers.TryGetValue(config.Id, out var queueManager))
                {
                    queueManager.EnqueueData(dataPoint);
                }
            }
            catch (Exception ex)
            {
                messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集任务异常：{ex.Message} - StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 检查 PLC 连接状态，若断开则尝试重连
        /// </summary>
        private async Task IfPlcClientNotConnectedReconnectAsync(DataAcquisitionConfig config)
        {
            var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";
            if (_plcClients.TryGetValue(plcKey, out var plcClient))
            {
                if (_plcConnectionStatus.TryGetValue(plcKey, out var isConnected) && isConnected)
                {
                    return;
                }

                var connect = await plcClient.ConnectServerAsync();
                if (connect.IsSuccess)
                {
                    _plcConnectionStatus[plcKey] = true;
                    messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 重新连接到 {plcKey} 成功！");
                }
                else
                {
                    _plcConnectionStatus[plcKey] = false;
                    // 这里可以选择抛出异常或仅记录错误，视具体业务场景而定
                    throw new Exception($"重新连接到设备 {plcKey} 失败：{connect.Message}");
                }
            }
        }

        /// <summary>
        /// 读取所有寄存器数据，若部分读取失败则记录错误（不影响其它寄存器数据）
        /// </summary>
        private async Task<DataPoint?> ReadAsync(DataAcquisitionConfig config)
        {
            var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";
            if (!_plcClients.TryGetValue(plcKey, out var plcClient))
            {
                return null;
            }

            var data = new Dictionary<string, object>();

            foreach (var register in config.Plc.Registers)
            {
                try
                {
                    var result = await ParseValue(plcClient, register.DataAddress, register.DataLength, register.DataType);
                    if (result.IsSuccess)
                    {
                        var value = await ContentHandle(register, result.Content);
                        data[register.ColumnName] = value;
                    }
                    else
                    {
                        messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取设备 {config.Plc.Code} 寄存器地址 {register.DataAddress} 失败：{result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取寄存器 {register.DataAddress} 异常：{ex.Message}");
                }
            }

            if (data.Count == 0)
            {
                return null;
            }

            var dataPoint = new DataPoint(data);
            processDataPoint(dataPoint, config);
            return dataPoint;
        }

        /// <summary>
        /// 对读取到的数据进行表达式计算处理
        /// </summary>
        private static async Task<object?> ContentHandle(Register register, object content)
        {
            if (string.IsNullOrWhiteSpace(register.EvalExpression))
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

            return await expression.EvaluateAsync();
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
                throw new ArgumentException($"不支持的数据类型：{dataType}");
            }
        }

        /// <summary>
        /// 停止所有数据采集任务并释放相关资源
        /// </summary>
        public async Task StopCollectionTasks()
        {
            // 先取消所有采集任务
            foreach (var kvp in _runningTasks)
            {
                kvp.Value.Cts.Cancel();
            }
            try
            {
                // 等待所有任务退出
                await Task.WhenAll(_runningTasks.Values.Select(x => x.Task));
            }
            catch (Exception)
            {
                // 忽略取消操作产生的异常
            }
            _runningTasks.Clear();

            // 释放信号量
            foreach (var semaphore in _locks.Values)
            {
                semaphore.Dispose();
            }
            _locks.Clear();

            // 关闭并清理所有 PLC 客户端
            foreach (var plcClient in _plcClients.Values)
            {
                await plcClient.ConnectCloseAsync();
            }
            _plcClients.Clear();
            _plcConnectionStatus.Clear();

            // 通知队列管理器完成工作，并清理
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
