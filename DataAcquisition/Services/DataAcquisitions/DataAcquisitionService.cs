using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Common;
using DataAcquisition.Services.DataAcquisitionConfigs;
using DataAcquisition.Services.QueueManagers;
using NCalc;

namespace DataAcquisition.Services.DataAcquisitions;

/// <summary>
/// 数据采集器
/// </summary>
public class DataAcquisitionService : IDataAcquisitionService
{
    private readonly IDataAcquisitionConfigService _dataAcquisitionConfigService;
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _runningTasks = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<string, IPlcClient> _plcClients = new();
    private readonly ConcurrentDictionary<string, bool> _plcConnectionStatus = new();
    private readonly ConcurrentDictionary<int, IQueueManager> _queueManagers = new();
    private readonly PlcClientFactory _plcClientFactory;
    private readonly DataStorageFactory _dataStorageFactory;
    private readonly QueueManagerFactory _queueManagerFactory;
    private readonly ProcessDataPoint _processDataPoint;
    private readonly MessageHandle _messageHandle;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dataAcquisitionConfigService"></param>
    /// <param name="plcClientFactory"></param>
    /// <param name="dataStorageFactory"></param>
    /// <param name="queueManagerFactory"></param>
    /// <param name="processDataPoint"></param>
    /// <param name="messageHandle"></param>
    public DataAcquisitionService(
        IDataAcquisitionConfigService dataAcquisitionConfigService,
        PlcClientFactory plcClientFactory,
        DataStorageFactory dataStorageFactory,
        QueueManagerFactory queueManagerFactory,
        ProcessDataPoint processDataPoint, 
        MessageHandle messageHandle)
    {
        _dataAcquisitionConfigService = dataAcquisitionConfigService;
        _plcClientFactory = plcClientFactory;
        _dataStorageFactory = dataStorageFactory;
        _queueManagerFactory = queueManagerFactory;
        _processDataPoint = processDataPoint;
        _messageHandle = messageHandle;
    }
    
    /// <summary>
    /// 开始采集任务
    /// </summary>
    public async Task StartCollectionTasks()
    {
        var dataAcquisitionConfigs = await _dataAcquisitionConfigService.GetConfigs();
        
        foreach (var config in dataAcquisitionConfigs)
        {
            if (!config.IsEnabled) continue;
            
            StartCollectionTask(config);
        }
    }

    /// <summary>
    /// 开始单个采集任务
    /// </summary>
    /// <param name="config"></param>
    private void StartCollectionTask(DataAcquisitionConfig config)
    {
        if (_runningTasks.ContainsKey(config.Id))
        {
            return;
        }
        
        var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";
        _plcConnectionStatus[plcKey] = false;

        var ctx = new CancellationTokenSource();
        var token = ctx.Token;
        
        Task.Run(async () =>
        { 
            await CreatePlcClientAsync(config); 
            
            InitializeQueueManager(config);

            while (!token.IsCancellationRequested)
            {
                await DataCollectAsync(config);
                await Task.Delay(config.CollectionFrequency, token).ConfigureAwait(false);
            }
        }, token);
        
        _runningTasks[config.Id] = ctx;
    }

    /// <summary>
    /// 创建 PLC 客户端
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private async Task CreatePlcClientAsync(DataAcquisitionConfig config)
    {
        var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";

        if (_plcClients.ContainsKey(plcKey))
        {
            return;
        }

        var semaphore = _locks.GetOrAdd(plcKey, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        
        try
        {
            var plcClient = _plcClients.GetOrAdd(plcKey, _ => _plcClientFactory(config.Plc.IpAddress, config.Plc.Port));

            var connect = await plcClient.ConnectServerAsync();

            if (connect.IsSuccess)
            {
                _plcConnectionStatus[plcKey] = true;
                _messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {plcKey} 成功！");
            }
            else
            {
                _plcConnectionStatus[plcKey] = false;
                _messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {plcKey} 失败：{connect.Message}");
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// 队列管理初始化
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private void InitializeQueueManager(DataAcquisitionConfig config)
    {
        _queueManagers.GetOrAdd(config.Id, _ => _queueManagerFactory(_dataStorageFactory, config));
    }

    /// <summary>
    /// 收集数据并处理异常
    /// </summary>
    /// <param name="config"></param>
    private async Task DataCollectAsync(DataAcquisitionConfig config)
    {
        try
        {
            await IfPlcClientNotConnectedReconnectAsync(config);
            var dataPoint = await ReadAsync(config);
            if (dataPoint != null && _queueManagers.TryGetValue(config.Id, out var queueManager))
            {
                queueManager.EnqueueData(dataPoint);
            }
        }
        catch (Exception ex)
        {
            _messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {ex.Message} - StackTrace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 如果 PLC 客户端连接断开则重连
    /// </summary>
    /// <param name="config"></param>
    /// <exception cref="Exception"></exception>
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
                _messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 重新连接到 {plcKey} 成功！");
            }
            else
            {
                _plcConnectionStatus[plcKey] = false;
                throw new Exception($"重新连接到设备 {plcKey} 失败：{connect.Message}");
            }
        }
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private async Task<DataPoint?> ReadAsync(DataAcquisitionConfig config)
    {
        var plcKey = $"{config.Plc.IpAddress}:{config.Plc.Port}";
        
        if (!_plcClients.TryGetValue(plcKey, out var plcClient)) return null;
        
        var data = new Dictionary<string, object>();

        foreach (var register in config.Plc.Registers)
        {
            var result = await ParseValue(plcClient, register.DataAddress,
                register.DataLength, register.DataType);

            if (result.IsSuccess)
            {
                data[register.ColumnName] = await ContentHandle(register, result.Content);
            }
            else
            {
                throw new Exception($"读取设备 {config.Plc.Code} 寄存器地址 {register.DataAddress} 失败：{result.Message}");
            }
        }

        if (data.Count == 0)
        {
            return null;
        }

        var dataPoint = new DataPoint(data);
        _processDataPoint(dataPoint, config);
        return dataPoint;

    }

    /// <summary>
    /// 内容处理
    /// </summary>
    /// <param name="register"></param>
    /// <param name="content"></param>
    private static async Task<object?> ContentHandle(Register register, object content)
    {
        if (string.IsNullOrWhiteSpace(register.EvalExpression)) return content;
        
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
    /// 读取数据
    /// </summary>
    /// <param name="plcClient"></param>
    /// <param name="dataAddress"></param>
    /// <param name="dataLength"></param>
    /// <param name="dataType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private async Task<OperationResult> ParseValue(IPlcClient plcClient, string dataAddress, ushort dataLength, string dataType)
    {
        var operations = new Dictionary<string, Func<Task<OperationResult>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ushort"] = async () => OperationResult.From(await plcClient.ReadUInt16Async(dataAddress)),
            ["uint"] = async () => OperationResult.From(await plcClient.ReadUInt32Async(dataAddress)),
            ["ulong"] = async () => OperationResult.From(await plcClient.ReadUInt64Async(dataAddress)),
            ["short"] = async () => OperationResult.From(await plcClient.ReadInt16Async(dataAddress)),
            ["int"] = async () => OperationResult.From(await plcClient.ReadInt32Async(dataAddress)),
            ["long"] = async () => OperationResult.From(await plcClient.ReadInt64Async(dataAddress)),
            ["float"] = async () => OperationResult.From(await plcClient.ReadFloatAsync(dataAddress)),
            ["double"] = async () => OperationResult.From(await plcClient.ReadDoubleAsync(dataAddress)),
            ["string"] = async () => OperationResult.From(await plcClient.ReadStringAsync(dataAddress, dataLength)),
            ["bool"] = async () => OperationResult.From(await plcClient.ReadBoolAsync(dataAddress))
        };
        return operations.TryGetValue(dataType, out var operation) ? await operation() : throw new ArgumentException($"不支持的数据类型：{dataType}");
    }

    /// <summary>
    /// 停止数据采集任务
    /// </summary>
    public async Task StopCollectionTasks()
    {    
        foreach (var ctx in _runningTasks.Values)
        {
            ctx.Cancel();
            ctx.Dispose();
        }
        _runningTasks.Clear();
        
        foreach (var semaphoreSlim in _locks.Values)
        {
            semaphoreSlim.Dispose();
        }
        _locks.Clear();
        
        foreach (var plcClient in _plcClients.Values)
        {
            await plcClient.ConnectCloseAsync();
        }
        _plcClients.Clear();
        
        _plcConnectionStatus.Clear();

        foreach (var queueManager in _queueManagers.Values)
        {
            queueManager.Complete();
        }
        _queueManagers.Clear();
    }
    
    /// <summary>
    /// 获取 PLC 连接状态
    /// </summary>
    /// <returns></returns>
    public SortedDictionary<string, bool> GetPlcConnectionStatus()
    {
        return new SortedDictionary<string, bool>(_plcConnectionStatus);
    }
}