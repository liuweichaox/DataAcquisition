using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Common;
using DataAcquisition.Services.DataAcquisitionConfigs;
using DataAcquisition.Services.QueueManagers;

namespace DataAcquisition.Services.DataAcquisitions;

/// <summary>
/// 数据采集器
/// </summary>
public class DataAcquisitionService : IDataAcquisitionService
{
    private readonly IDataAcquisitionConfigService _dataAcquisitionConfigService;
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentDictionary<string, IPlcClient> _plcClients;
    private readonly ConcurrentBag<IQueueManager> _queueManagers;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly PlcClientFactory _plcClientFactory;
    private readonly DataStorageFactory _dataStorageFactory;
    private readonly ProcessReadData _processReadData;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dataAcquisitionConfigService"></param>
    /// <param name="plcClientFactory"></param>
    /// <param name="dataStorageFactory"></param>
    /// <param name="processReadData"></param>
    public DataAcquisitionService(
        IDataAcquisitionConfigService dataAcquisitionConfigService,
        PlcClientFactory plcClientFactory,
        DataStorageFactory dataStorageFactory,
        ProcessReadData processReadData)
    {
        _dataAcquisitionConfigService = dataAcquisitionConfigService;
        _cts = new CancellationTokenSource();
        _plcClients = new ConcurrentDictionary<string, IPlcClient>();
        _queueManagers = new ConcurrentBag<IQueueManager>();
        _plcClientFactory = plcClientFactory;
        _dataStorageFactory = dataStorageFactory;
        _processReadData = processReadData;
    }
    
    /// <summary>
    /// 开始采集任务
    /// </summary>
    public async Task StartCollectionTasks()
    {
        var tasks = new List<Task>();
         
        var dataAcquisitionConfigs = await _dataAcquisitionConfigService.GetDataAcquisitionConfigs();
        
        foreach (var config in dataAcquisitionConfigs)
        {
            if (!config.IsEnabled) continue;
            
            var task = StartCollectionTask(config);
            tasks.Add(task);
        }
            
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 开始单个采集任务
    /// </summary>
    /// <param name="config"></param>
    private async Task StartCollectionTask(DataAcquisitionConfig config)
    {
        var task = Task.Run(async () =>
        {
            var plcClient = await CreatePlcClientWithRetryAsync(config); 
            
            var queueManager = InitializeQueueManager(config);

            while (!_cts.Token.IsCancellationRequested)
            {
                await CollectDataAsync(config, plcClient, queueManager);
                await Task.Delay(config.CollectionFrequency, _cts.Token).ConfigureAwait(false);
            }
        }, _cts.Token);

        await task.ConfigureAwait(false);
    }
    
    /// <summary>
    /// 创建 PLC 客户端并进行重试
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private async Task<IPlcClient> CreatePlcClientWithRetryAsync(DataAcquisitionConfig config)
    {
        IPlcClient plcClient = null;
        
        while (plcClient == null)
        {
            try
            {
                plcClient = await CreatePlcClientAsync(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 正在重试：{ex.Message}. 正在重试");
                await Task.Delay(1000);
            }
        }

        return plcClient;
    }
    
    /// <summary>
    /// 创建 PLC 客户端
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private async Task<IPlcClient> CreatePlcClientAsync(DataAcquisitionConfig config)
    {
        if (_plcClients.TryGetValue(config.Code, out var plcClient))
        {
            return plcClient;
        }
        
        var semaphore = _locks.GetOrAdd(config.Code, _ => new SemaphoreSlim(1, 1));
        
        await semaphore.WaitAsync();

        try
        {
            if (_plcClients.TryGetValue(config.Code, out plcClient))
            {
                return plcClient;
            }
            
            plcClient = _plcClientFactory(config.IpAddress, config.Port);
            var connect = await RetryOnFailure(() => plcClient.ConnectServerAsync());
        
            if (connect.IsSuccess)
            {
                Console.WriteLine($"连接到设备 {config.Code} 成功！");
                _plcClients.TryAdd(config.Code, plcClient);
                return plcClient;
            }
            else
            {
                throw new Exception($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {config.Code} 失败：{connect.Message}");
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
    private QueueManager InitializeQueueManager(DataAcquisitionConfig config)
    {
        var queueManager = new QueueManager(_dataStorageFactory, config);
        _queueManagers.Add(queueManager);
        return queueManager;
    }

    /// <summary>
    /// 收集数据并处理异常
    /// </summary>
    /// <param name="config"></param>
    /// <param name="plcClient"></param>
    /// <param name="queueManager"></param>
    private async Task CollectDataAsync(DataAcquisitionConfig config, IPlcClient plcClient, QueueManager queueManager)
    {
        try
        {
            await IfPLCClientNotConnectedReconnectAsync(config, plcClient);
            var data = await ReadAsync(config, plcClient);
            queueManager.EnqueueData(data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集数据异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 如果 PLC 客户端连接断开则重连
    /// </summary>
    /// <param name="config"></param>
    /// <param name="plcClient"></param>
    /// <exception cref="Exception"></exception>
    private async Task IfPLCClientNotConnectedReconnectAsync(DataAcquisitionConfig config, IPlcClient plcClient)
    {
        if (!plcClient.IsConnected())
        {
            var connect = await RetryOnFailure(() => plcClient.ConnectServerAsync());
            if (connect.IsSuccess)
            {
                Console.WriteLine($"重新连接到设备 {config.Code} 成功！");
            }
            else
            {
                throw new Exception($"重新连接到设备 {config.Code} 失败：{connect.Message}");
            }
        }
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="config"></param>
    /// <param name="plcClient"></param>
    /// <returns></returns>
    private async Task<Dictionary<string, object>> ReadAsync(
        DataAcquisitionConfig config,
        IPlcClient plcClient)
    {
        var data = new Dictionary<string, object>();

        foreach (var positionConfig in config.PositionConfigs)
        {
            var result = await ParseValue(plcClient, positionConfig.DataAddress,
                positionConfig.DataLength, positionConfig.DataType);

            if (result.IsSuccess)
            {
                data[positionConfig.ColumnName] = result.Content;
            }
            else
            {
                throw new Exception($"读取设备 {config.Code} 失败：{result.Message}");
            }
        }

        _processReadData(data, config);

        return data;
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
        else if (dataType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
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
        _cts.Cancel();

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