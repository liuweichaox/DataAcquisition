using DynamicPLCDataCollector;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.Services.DataStorages;
using DynamicPLCDataCollector.Services.PLCClients;

IPLCClient PLCClientFactory(string ipAddress, int port) => new PLCClient(ipAddress, port);

IDataStorage DataStorageFactory(MetricTableConfig metricTableConfig) => new SQLiteDataStorage(metricTableConfig);

var dataCollector = new DataCollector(PLCClientFactory, DataStorageFactory);

await dataCollector.StartCollectionTasks();

await Task.Delay(Timeout.Infinite);
