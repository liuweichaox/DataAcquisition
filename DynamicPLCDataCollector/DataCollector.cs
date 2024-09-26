using System.Collections.Concurrent;
using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.PLCClients;
using DynamicPLCDataCollector.Services;

/// <summary>
/// 数据采集器
/// </summary>
public class DataCollector
{
    private readonly IPLCClientManager _clientManager;
    private readonly IDataStorage _dataStorage;
    private readonly IDeviceService _deviceService;
    private readonly IMetricTableConfigService _metricTableConfigService;
    private readonly ConcurrentDictionary<string, Task> _runningTasks;
    private readonly CancellationTokenSource _cts;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientManager"></param>
    /// <param name="dataStorage"></param>
    public DataCollector(IPLCClientManager clientManager, IDataStorage dataStorage)
    {
        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集程序已启动...");
        _clientManager = clientManager;
        _dataStorage = dataStorage;
        _deviceService = new DeviceService();
        _metricTableConfigService = new MetricTableConfigService();
        _runningTasks  = new ConcurrentDictionary<string, Task>();
        _cts = new CancellationTokenSource();
        ListenExitEvents();
    }
    
    /// <summary>
    /// 生成采集任务的 Key
    /// </summary>
    /// <param name="device"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    private string GenerateTaskKey(Device device, MetricTableConfig config)
    {
        return $"{device.Code}_{config.TableName}";
    }
    
    /// <summary>
    /// 是否开始采集任务
    /// </summary>
    /// <param name="device"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    private bool IsTaskRunningForDeviceAndConfig(Device device, MetricTableConfig config)
    {
        var taskKey = GenerateTaskKey(device, config);
        return _runningTasks.ContainsKey(taskKey);
    }
    
    /// <summary>
    /// 开始采集任务
    /// </summary>
    public async Task StartCollectionTasks()
    {
        var devices = await _deviceService.GetDevices();
        
        var metricTableConfigs = await _metricTableConfigService.GetMetricTableConfigs();
        
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
    }

    /// <summary>
    /// 开始单个采集任务
    /// </summary>
    /// <param name="device"></param>
    /// <param name="metricTableConfig"></param>
    private void StartCollectionTask(Device device, MetricTableConfig metricTableConfig)
    {
        var task = Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                try
                {
                    var data = await _clientManager.ReadAsync(device, metricTableConfig);
                    _dataStorage.Save(data, metricTableConfig);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集数据异常: {ex.Message}");
                }
                await Task.Delay(metricTableConfig.CollectionFrequency, _cts.Token);
            }
        }, TaskCreationOptions.LongRunning);
        
        var taskKey = GenerateTaskKey(device, metricTableConfig);
        _runningTasks[taskKey] = task;
    }

    /// <summary>
    /// 监听退出事件
    /// </summary>
    public void ListenExitEvents()
    {
        Console.CancelKeyPress += async (sender, e) =>
        {
            e.Cancel = true;
            await HandleExitAsync();
        };
        AppDomain.CurrentDomain.ProcessExit += async (s, e) => await HandleExitAsync();
    }

    /// <summary>
    /// 处理退出
    /// </summary>
    private async Task HandleExitAsync()
    {
        _cts.Cancel();

        await Task.Run(async () =>
        {
            await _clientManager.DisconnectAllAsync();
            _dataStorage.ReleaseAll();
            LogExitInformation("程序已正常退出");
        });
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
