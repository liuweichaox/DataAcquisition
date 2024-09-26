using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.PLCClients;
using DynamicPLCDataCollector.Utils;

Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集程序已启动...");

var devices = await JsonUtils.LoadConfigAsync<List<Device>>("Configs/devices.json");

var metricTableConfigs = await JsonUtils.LoadAllJsonFilesAsync<MetricTableConfig>("Configs/MetricConfigs");

IPLCClientManager clientManager = new PLCClientManager(devices);

IDataStorage dataStorage = new SQLiteDataStorage();

var dataCollector = new DataCollector(clientManager, dataStorage);

await dataCollector.StartCollectionTasks(devices, metricTableConfigs);