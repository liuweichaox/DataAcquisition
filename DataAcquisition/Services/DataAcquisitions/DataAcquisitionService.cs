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
    private readonly ConcurrentDictionary<string, Task> _runningTasks;
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentBag<IPLCClient> _plcClients;
    private readonly ConcurrentBag<IDataStorage> _dataStorages;
    private readonly ConcurrentBag<IQueueManager> _queueManagers;
    private readonly PLCClientFactory _plcClientFactory;
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
        PLCClientFactory plcClientFactory,
        DataStorageFactory dataStorageFactory,
        ProcessReadData processReadData)
    {
        _dataAcquisitionConfigService = dataAcquisitionConfigService;
        _runningTasks = new ConcurrentDictionary<string, Task>();
        _cts = new CancellationTokenSource();
        _plcClients = new ConcurrentBag<IPLCClient>();
        _dataStorages = new ConcurrentBag<IDataStorage>();
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
        var dataAcquisitionConfigs = await _dataAcquisitionConfigService.GetDataAcquisitionConfigs();

        foreach (var config in dataAcquisitionConfigs)
        {
            if (config.IsEnabled && !IsTaskRunningForDeviceAndConfig(config))
            {
                StartCollectionTask(config);
            }
        }

        await Task.Delay(Timeout.Infinite);
    }

    /// <summary>
    /// 开始单个采集任务
    /// </summary>
    /// <param name="config"></param>
    private void StartCollectionTask(DataAcquisitionConfig config)
    {
        var task = Task.Factory.StartNew(async () =>
        {
            var plcClient = await CreatePLCClientAsync(config);
            var dataStorage = CreateDataStorage(config);
            var queueManager = new QueueManager(dataStorage, config);
            _queueManagers.Add(queueManager);

            while (true)
            {
                await ReadAndSaveAsync(config, plcClient, queueManager);
                await Task.Delay(config.CollectionFrequency, _cts.Token);
            }
        }, TaskCreationOptions.LongRunning);

        var taskKey = GenerateTaskKey(config);
        _runningTasks[taskKey] = task;
    }

    /// <summary>
    /// 创建 PLC 客户端
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private async Task<IPLCClient> CreatePLCClientAsync(DataAcquisitionConfig config)
    {
        var plcClient = _plcClientFactory(config.IpAddress, config.Port);
        var connect = await plcClient.ConnectServerAsync();
        if (connect.IsSuccess)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {config.Code} 成功！");
        }
        else
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {config.Code} 失败：{connect.Message}");
        }

        _plcClients.Add(plcClient);

        return plcClient;
    }

    /// <summary>
    /// 创建数据存储服务
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private IDataStorage CreateDataStorage(DataAcquisitionConfig config)
    {
        var dataStorage = _dataStorageFactory(config);
        _dataStorages.Add(dataStorage);
        return dataStorage;
    }

    /// <summary>
    /// 读取数据并保存
    /// </summary>
    /// <param name="config"></param>
    /// <param name="plcClient"></param>
    /// <param name="queueManager"></param>
    private async Task ReadAndSaveAsync(
        DataAcquisitionConfig config,
        IPLCClient plcClient,
        QueueManager queueManager)
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
    private static async Task IfPLCClientNotConnectedReconnectAsync(DataAcquisitionConfig config, IPLCClient plcClient)
    {
        if (!plcClient.IsConnected())
        {
            var connect = await plcClient.ConnectServerAsync();
            if (connect.IsSuccess)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 重新连接到设备 {config.Code} 成功！");
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
        IPLCClient plcClient)
    {
        var data = new Dictionary<string, object>();

        foreach (var positionConfig in config.PositionConfigs)
        {
            try
            {
                data[positionConfig.ColumnName] = await ParseValue(plcClient, positionConfig.DataAddress,
                    positionConfig.DataLength, positionConfig.DataType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取设备 {config.Code} 失败：{ex.Message}");
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
    private async Task<object> ParseValue(
        IPLCClient plcClient,
        string dataAddress,
        ushort dataLength,
        string dataType)
    {
        return dataType.ToLower() switch
        {
            "ushort" => (await RetryOnFailure(() => plcClient.ReadUInt16Async(dataAddress))).Content,
            "uint" => (await RetryOnFailure(() => plcClient.ReadUInt32Async(dataAddress))).Content,
            "ulong" => (await RetryOnFailure(() => plcClient.ReadUInt64Async(dataAddress))).Content,
            "short" => (await RetryOnFailure(() => plcClient.ReadInt16Async(dataAddress))).Content,
            "int" => (await RetryOnFailure(() => plcClient.ReadInt32Async(dataAddress))).Content,
            "long" => (await RetryOnFailure(() => plcClient.ReadInt64Async(dataAddress))).Content,
            "float" => (await RetryOnFailure(() => plcClient.ReadFloatAsync(dataAddress))).Content,
            "double" => (await RetryOnFailure(() => plcClient.ReadDoubleAsync(dataAddress))).Content,
            "string" => (await RetryOnFailure(() => plcClient.ReadStringAsync(dataAddress, dataLength))).Content,
            "boolean" => (await RetryOnFailure(() => plcClient.ReadBoolAsync(dataAddress))).Content,
            _ => throw new ArgumentException("未知的数据类型")
        };
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
        var retries = 0;

        while (retries < maxRetries)
        {
            var result = await action();
            if (result.IsSuccess)
            {
                return result;
            }

            retries++;
            await Task.Delay(1000); // 等待1秒后重试
        }

        throw new Exception($"操作失败，已达到最大重试次数 {maxRetries}。");
    }

    /// <summary>
    /// 生成采集任务的 Key
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private string GenerateTaskKey(DataAcquisitionConfig config)
    {
        return $"{config.Code}_{config.TableName}";
    }

    /// <summary>
    /// 是否开始采集任务
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    private bool IsTaskRunningForDeviceAndConfig(DataAcquisitionConfig config)
    {
        var taskKey = GenerateTaskKey(config);
        return _runningTasks.ContainsKey(taskKey);
    }

    public async Task StopCollectionTasks()
    {
        _cts.Cancel();

        foreach (var plcClient in _plcClients)
        {
            await plcClient.ConnectCloseAsync();
        }

        foreach (var dataStorage in _dataStorages)
        {
            await dataStorage.DisposeAsync();
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
        var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}\n";
        File.AppendAllText(logFilePath, logMessage);
    }
}