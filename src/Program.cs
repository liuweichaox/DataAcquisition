IPLCClient PLCClientFactory(string ipAddress, int port) => new PLCClient(ipAddress, port);

IDataStorage DataStorageFactory(Device device, MetricTableConfig metricTableConfig) => new SQLiteDataStorage(device, metricTableConfig);

var dataCollector = new DataCollector(PLCClientFactory, DataStorageFactory);

await dataCollector.StartCollectionTasks();

await Task.Delay(Timeout.Infinite);
