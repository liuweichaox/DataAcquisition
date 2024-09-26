using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.PLCClients;

/// <summary>
/// 数据采集器
/// </summary>
public class DataCollector
{
    private readonly IPLCClientManager _clientManager;
    private readonly IDataStorage _dataStorage;
    private readonly CancellationTokenSource _cts;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientManager"></param>
    /// <param name="dataStorage"></param>
    public DataCollector(IPLCClientManager clientManager, IDataStorage dataStorage)
    {
        _clientManager = clientManager;
        _dataStorage = dataStorage;
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// 开始采集任务
    /// </summary>
    /// <param name="devices"></param>
    /// <param name="metricTableConfigs"></param>
    public async Task StartCollectionTasks(List<Device> devices, List<MetricTableConfig> metricTableConfigs)
    {
        ListenExitEvents();
        
        foreach (var device in devices)
        {
            foreach (var metricTableConfig in metricTableConfigs)
            {
                if (metricTableConfig.IsEnabled)
                {
                   StartCollectionTask(device, metricTableConfig);
                }
            }
        }
        
        while (true)
        {
            await Task.Delay(1000);
        }
    }

    /// <summary>
    /// 开始单个采集任务
    /// </summary>
    /// <param name="device"></param>
    /// <param name="metricTableConfig"></param>
    private void StartCollectionTask(Device device, MetricTableConfig metricTableConfig)
    {
        Task.Factory.StartNew(async () =>
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
