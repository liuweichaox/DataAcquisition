using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.PLCClients;

public class DataCollector
{
    private readonly IPLCClientManager _clientManager;
    private readonly IDataStorage _dataStorage;
    private readonly CancellationTokenSource _cts;

    public DataCollector(IPLCClientManager clientManager, IDataStorage dataStorage)
    {
        _clientManager = clientManager;
        _dataStorage = dataStorage;
        _cts = new CancellationTokenSource();
    }

    public void StartCollectionTasks(List<Device> devices, List<MetricTableConfig> metricTableConfigs)
    {
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
    }

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

    public void ListenExitEvents()
    {
        Console.CancelKeyPress += async (sender, e) =>
        {
            e.Cancel = true;
            await HandleExitAsync();
        };
        AppDomain.CurrentDomain.ProcessExit += async (s, e) => await HandleExitAsync();
        
    }

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

    private void LogExitInformation(string message)
    {
        var logFilePath = Path.Combine(AppContext.BaseDirectory, "exit_log.txt");
        var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}\n";
        File.AppendAllText(logFilePath, logMessage);
    }
}
