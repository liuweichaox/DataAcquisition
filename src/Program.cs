var dataCollector = new DataCollector(PLCClientFactory, DataStorageFactory, ProcessReadData);

await dataCollector.StartCollectionTasks();

await Task.Delay(Timeout.Infinite);

IPLCClient PLCClientFactory(string ipAddress, int port) => new PLCClient(ipAddress, port);

IDataStorage DataStorageFactory(Device device, MetricTableConfig metricTableConfig) => new SQLiteDataStorage(device, metricTableConfig);

void ProcessReadData(Dictionary<string, object> data, Device device)
{
    data["时间"] = DateTime.Now;
    data["DeviceCode"] = device.Code;
}