using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.Services;
using DynamicPLCDataCollector.Utils;

Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集程序已启动...");

var devices = await JsonUtils.LoadConfigAsync<List<Device>>("Configs/devices.json");

var metricTableConfigs =await JsonUtils.LoadAllJsonFilesAsync<MetricTableConfig>("Configs/MetricConfigs");

IPLCCommunicator communicator = new PLCCommunicator(devices);

IDataStorage dataStorage = new SQLiteDataStorage();

foreach (var device in devices)
{
    foreach (var metricTableConfig in metricTableConfigs)
    {
        StartCollectionTask(device, metricTableConfig);
    }
}

while (true)
{
    await Task.Delay(1000);
}

void StartCollectionTask(Device device, MetricTableConfig metricTableConfig)
{
    Task.Factory.StartNew(async () =>
    {
        while (true)
        {
            try
            {
                if (metricTableConfig.IsEnabled)
                {
                    var data = await communicator.ReadAsync(device, metricTableConfig);
                    dataStorage.Save(data, metricTableConfig);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集数据异常: {ex.Message}");
            }
            await Task.Delay(metricTableConfig.CollectionFrequency);
        }
    }, TaskCreationOptions.LongRunning);
}