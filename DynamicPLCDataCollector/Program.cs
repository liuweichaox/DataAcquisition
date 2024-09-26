using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.PLCClients;
using DynamicPLCDataCollector.Utils;

Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集程序已启动...");

var baseDirectory = AppContext.BaseDirectory;
var deviceConfigPath = Path.Combine(baseDirectory, "Configs", "devices.json");
var metricTableConfigPath = Path.Combine(baseDirectory, "Configs", "MetricConfigs");

var devices = await JsonUtils.LoadConfigAsync<List<Device>>(deviceConfigPath);

var metricTableConfigs = await JsonUtils.LoadAllJsonFilesAsync<MetricTableConfig>(metricTableConfigPath);

IPLCClientManager clientManager = new PLCClientManager(devices);

IDataStorage dataStorage = new SQLiteDataStorage();

var dataCollector = new DataCollector(clientManager, dataStorage);

dataCollector.ListenExitEvents();

dataCollector.StartCollectionTasks(devices, metricTableConfigs);

while (true)
{
    await Task.Delay(1000);
}