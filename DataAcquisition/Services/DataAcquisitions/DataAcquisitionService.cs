using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Common;
using DataAcquisition.Services.DataAcquisitionConfigs;
using DataAcquisition.Services.Devices;
using DataAcquisition.Services.QueueManagers;

namespace DataAcquisition.Services.DataAcquisitions;

/// <summary>
/// 数据采集器
/// </summary>
public class DataAcquisitionService : IDataAcquisitionService
{
    private readonly IDeviceService _deviceService;
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
    /// <param name="deviceService"></param>
    /// <param name="dataAcquisitionConfigService"></param>
    /// <param name="plcClientFactory"></param>
    /// <param name="dataStorageFactory"></param>
    /// <param name="processReadData"></param>
    public DataAcquisitionService(
        IDeviceService deviceService,
        IDataAcquisitionConfigService dataAcquisitionConfigService,
        PLCClientFactory plcClientFactory,
        DataStorageFactory dataStorageFactory,
        ProcessReadData processReadData)
    {
        _deviceService = deviceService;
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
        var devices = await _deviceService.GetDevices();
        var metricTableConfigs = await _dataAcquisitionConfigService.GetDataAcquisitionConfigs();

        foreach (var device in devices)
        {
            foreach (var metricTableConfig in metricTableConfigs)
            {
                if (metricTableConfig.IsEnabled && !IsTaskRunningForDeviceAndConfig(device, metricTableConfig))
                {
                    StartCollectionTask(device, metricTableConfig);
                }
            }
        }

        await Task.Delay(Timeout.Infinite);
    }

    /// <summary>
    /// 开始单个采集任务
    /// </summary>
    /// <param name="device"></param>
    /// <param name="dataAcquisitionConfig"></param>
    private void StartCollectionTask(Device device, DataAcquisitionConfig dataAcquisitionConfig)
    {
        var task = Task.Factory.StartNew(async () =>
        {
            var plcClient = await CreatePLCClientAsync(device);
            var dataStorage = CreateDataStorage(device, dataAcquisitionConfig);
            var queueManager = new QueueManager(dataStorage, dataAcquisitionConfig);
            _queueManagers.Add(queueManager);

            while (true)
            {
                await ReadAndSaveAsync(device, dataAcquisitionConfig, plcClient, queueManager);
                await Task.Delay(dataAcquisitionConfig.CollectionFrequency, _cts.Token);
            }
        }, TaskCreationOptions.LongRunning);

        var taskKey = GenerateTaskKey(device, dataAcquisitionConfig);
        _runningTasks[taskKey] = task;
    }

    /// <summary>
    /// 创建 PLC 客户端
    /// </summary>
    /// <param name="device"></param>
    /// <returns></returns>
    private async Task<IPLCClient> CreatePLCClientAsync(Device device)
    {
        var plcClient = _plcClientFactory(device.IpAddress, device.Port);
        var connect = await plcClient.ConnectServerAsync();
        if (connect.IsSuccess)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {device.Code} 成功！");
        }
        else
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {device.Code} 失败：{connect.Message}");
        }

        _plcClients.Add(plcClient);

        return plcClient;
    }

    /// <summary>
    /// 创建数据存储服务
    /// </summary>
    /// <param name="device"></param>
    /// <param name="dataAcquisitionConfig"></param>
    /// <returns></returns>
    private IDataStorage CreateDataStorage(Device device, DataAcquisitionConfig dataAcquisitionConfig)
    {
        var dataStorage = _dataStorageFactory(device, dataAcquisitionConfig);
        _dataStorages.Add(dataStorage);
        return dataStorage;
    }

    /// <summary>
    /// 读取数据并保存
    /// </summary>
    /// <param name="device"></param>
    /// <param name="dataAcquisitionConfig"></param>
    /// <param name="plcClient"></param>
    /// <param name="queueManager"></param>
    private async Task ReadAndSaveAsync(
        Device device,
        DataAcquisitionConfig dataAcquisitionConfig,
        IPLCClient plcClient,
        QueueManager queueManager)
    {
        try
        {
            await IfPLCClientNotConnectedReconnectAsync(device, plcClient);
            var data = await ReadAsync(device, dataAcquisitionConfig, plcClient);
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
    /// <param name="device"></param>
    /// <param name="plcClient"></param>
    /// <exception cref="Exception"></exception>
    private static async Task IfPLCClientNotConnectedReconnectAsync(Device device, IPLCClient plcClient)
    {
        if (!plcClient.IsConnected())
        {
            var connect = await plcClient.ConnectServerAsync();
            if (connect.IsSuccess)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 重新连接到设备 {device.Code} 成功！");
            }
            else
            {
                throw new Exception($"重新连接到设备 {device.Code} 失败：{connect.Message}");
            }
        }
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="device"></param>
    /// <param name="dataAcquisitionConfig"></param>
    /// <param name="plcClient"></param>
    /// <returns></returns>
    private async Task<Dictionary<string, object>> ReadAsync(
        Device device,
        DataAcquisitionConfig dataAcquisitionConfig,
        IPLCClient plcClient)
    {
        var data = new Dictionary<string, object>();

        foreach (var positionConfig in dataAcquisitionConfig.PositionConfigs)
        {
            try
            {
                data[positionConfig.ColumnName] = await ParseValue(plcClient, positionConfig.DataAddress,
                    positionConfig.DataLength, positionConfig.DataType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取设备 {device.Code} 失败：{ex.Message}");
            }
        }

        _processReadData(data, device);

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
            "int" => (await RetryOnFailure(() => plcClient.ReadInt32Async(dataAddress))).Content,
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
    /// <param name="device"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    private string GenerateTaskKey(
        Device device,
        DataAcquisitionConfig config)
    {
        return $"{device.Code}_{config.TableName}";
    }

    /// <summary>
    /// 是否开始采集任务
    /// </summary>
    /// <param name="device"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    private bool IsTaskRunningForDeviceAndConfig(
        Device device,
        DataAcquisitionConfig config)
    {
        var taskKey = GenerateTaskKey(device, config);
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