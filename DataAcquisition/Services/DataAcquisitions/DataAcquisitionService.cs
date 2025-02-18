using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
    private readonly ConcurrentDictionary<string, IPlcClient> _plcClients;
    private readonly ConcurrentBag<IQueueManager> _queueManagers;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly PlcClientFactory _plcClientFactory;
    private readonly DataStorageFactory _dataStorageFactory;
    private readonly QueueManagerFactory _queueManagerFactory;
    private readonly ProcessDataPoint _processDataPoint;
    private readonly MessageHandle _messageHandle;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTasks = new();

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
        _plcClients = new ConcurrentDictionary<string, IPlcClient>();
        _queueManagers = new ConcurrentBag<IQueueManager>();
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
        if (_runningTasks.ContainsKey(config.Plc.Code))
        {
            return;
        }
        
        var ctx = new CancellationTokenSource();
        var token = ctx.Token;
        Task.Run(async () =>
        {
            var plcClient = await CreatePlcClientAsync(config); 
            
            var queueManager = InitializeQueueManager(config);

            while (!token.IsCancellationRequested)
            {
                await CollectDataAsync(config, plcClient, queueManager);
                await Task.Delay(config.CollectionFrequency, token).ConfigureAwait(false);
            }
        }, token);
       
        _runningTasks.TryAdd(config.Plc.Code, ctx);
    }
    
    /// <summary>
    /// 创建 PLC 客户端
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private async Task<IPlcClient> CreatePlcClientAsync(DataAcquisitionConfig config)
    {
        if (_plcClients.TryGetValue(config.Plc.Code, out var plcClient))
        {
            return plcClient;
        }
        
        var semaphore = _locks.GetOrAdd(config.Plc.Code, _ => new SemaphoreSlim(1, 1));
        
        await semaphore.WaitAsync();

        try
        {
            plcClient =  _plcClients.GetOrAdd(config.Plc.Code, _ => _plcClientFactory(config.Plc.IpAddress, config.Plc.Port));
            
            var connect = await RetryOnFailure(() => plcClient.ConnectServerAsync());
        
            if (connect.IsSuccess)
            {
                _messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {config.Plc.Code} 成功！");
            }
            else
            {
                _messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {config.Plc.Code} 失败：{connect.Message}");
            }
            
            return plcClient;
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
    private IQueueManager InitializeQueueManager(DataAcquisitionConfig config)
    {
        var queueManager =  _queueManagerFactory(_dataStorageFactory, config);
        _queueManagers.Add(queueManager);
        return queueManager;
    }

    /// <summary>
    /// 收集数据并处理异常
    /// </summary>
    /// <param name="config"></param>
    /// <param name="plcClient"></param>
    /// <param name="queueManager"></param>
    private async Task CollectDataAsync(DataAcquisitionConfig config, IPlcClient plcClient, IQueueManager queueManager)
    {
        try
        {
            await IfPlcClientNotConnectedReconnectAsync(config, plcClient);
            var dataPoint = await ReadAsync(config, plcClient);
            if (dataPoint != null)
            {
                queueManager.EnqueueData(dataPoint);
            }
        }
        catch (Exception ex)
        {
            _messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {ex.Message}");
        }
    }

    /// <summary>
    /// 如果 PLC 客户端连接断开则重连
    /// </summary>
    /// <param name="config"></param>
    /// <param name="plcClient"></param>
    /// <exception cref="Exception"></exception>
    private async Task IfPlcClientNotConnectedReconnectAsync(DataAcquisitionConfig config, IPlcClient plcClient)
    {
        if (!plcClient.IsConnected())
        {
            var connect = await plcClient.ConnectServerAsync();
            if (connect.IsSuccess)
            {
                _messageHandle($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 重新连接到设备 {config.Plc.Code} 成功！");
            }
            else
            {
                throw new Exception($"重新连接到设备 {config.Plc.Code} 失败：{connect.Message}");
            }
        }
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="config"></param>
    /// <param name="plcClient"></param>
    /// <returns></returns>
    private async Task<DataPoint?> ReadAsync(
        DataAcquisitionConfig config,
        IPlcClient plcClient)
    {
        var data = new Dictionary<string, object>();

        foreach (var register in config.Plc.Registers)
        {
            var result = await ParseValue(plcClient, register.DataAddress,
                register.DataLength, register.DataType);

            if (result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(register.EvalExpression))
                {
                    var expression = new AsyncExpression(register.EvalExpression)
                    {
                        Parameters =
                        {
                            ["value"] = result.Content
                        }
                    };
                    var value = await expression.EvaluateAsync();
                    data[register.ColumnName] = value;
                }
                else
                {
                    data[register.ColumnName] = result.Content;
                }
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
    /// 读取数据
    /// </summary>
    /// <param name="plcClient"></param>
    /// <param name="dataAddress"></param>
    /// <param name="dataLength"></param>
    /// <param name="dataType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private async Task<OperationResult> ParseValue(
        IPlcClient plcClient,
        string dataAddress,
        ushort dataLength,
        string dataType)
    {
        OperationResult result = new OperationResult();

        if (dataType.Equals("ushort", StringComparison.OrdinalIgnoreCase))
        {
            var resultUshort = await plcClient.ReadUInt16Async(dataAddress);
            result = OperationResult.From(resultUshort);
        }
        else if (dataType.Equals("uint", StringComparison.OrdinalIgnoreCase))
        {
            var resultUint = await plcClient.ReadUInt32Async(dataAddress);
            result = OperationResult.From(resultUint);
        }
        else if (dataType.Equals("ulong", StringComparison.OrdinalIgnoreCase))
        {
            var resultUlong = await plcClient.ReadUInt64Async(dataAddress);
            result = OperationResult.From(resultUlong);
        }
        else if (dataType.Equals("short", StringComparison.OrdinalIgnoreCase))
        {
            var resultShort = await plcClient.ReadInt16Async(dataAddress);
            result = OperationResult.From(resultShort);
        }
        else if (dataType.Equals("int", StringComparison.OrdinalIgnoreCase))
        {
            var resultInt = await plcClient.ReadInt32Async(dataAddress);
            result = OperationResult.From(resultInt);
        }
        else if (dataType.Equals("long", StringComparison.OrdinalIgnoreCase))
        {
            var resultLong = await plcClient.ReadInt64Async(dataAddress);
            result = OperationResult.From(resultLong);
        }
        else if (dataType.Equals("float", StringComparison.OrdinalIgnoreCase))
        {
            var resultFloat = await plcClient.ReadFloatAsync(dataAddress);
            result = OperationResult.From(resultFloat);
        }
        else if (dataType.Equals("double", StringComparison.OrdinalIgnoreCase))
        {
            var resultDouble = await plcClient.ReadDoubleAsync(dataAddress);
            result = OperationResult.From(resultDouble);
        }
        else if (dataType.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            var resultString = await plcClient.ReadStringAsync(dataAddress, dataLength);
            result = OperationResult.From(resultString);
        }
        else if (dataType.Equals("bool", StringComparison.OrdinalIgnoreCase))
        {
            var resultBool = await plcClient.ReadBoolAsync(dataAddress);
            result = OperationResult.From(resultBool);
        }

        return result;
    }

    /// <summary>
    /// 失败重试读取
    /// </summary>
    /// <param name="action"></param>
    /// <param name="maxRetries"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private async Task<OperationResult<T>> RetryOnFailure<T>(
        Func<Task<OperationResult<T>>> action,
        int maxRetries = 3)
    {
        var result = new OperationResult<T>()
        {
            IsSuccess = false,
            Message = $"操作失败，已达到最大重试次数 {maxRetries}。"
        };
        var retries = 0;

        while (retries < maxRetries)
        {
            result = await action();
            if (result.IsSuccess)
            {
                return result;
            }

            retries++;
            await Task.Delay(1000); // 等待1秒后重试
        }

        return result;
    }
    
    public async Task StopCollectionTasks()
    {
        foreach (var token in _runningTasks.Values)
        {
            token.Cancel();
            token.Dispose();
        }
        _runningTasks.Clear();

        foreach (var plcClient in _plcClients.Values)
        {
            await plcClient.ConnectCloseAsync();
        }

        foreach (var queueManager in _queueManagers)
        {
            queueManager.Complete();
        }
        
        LogExitInformation("程序已正常退出");
    }

    /// <summary>
    /// 打印退出日志文件
    /// </summary>
    /// <param name="message"></param>
    private void LogExitInformation(string message)
    {
        var logFilePath = Path.Combine(AppContext.BaseDirectory, "exit_log.txt");
        var logMessage = $"{message}\n";
        File.AppendAllText(logFilePath, logMessage);
    }
}