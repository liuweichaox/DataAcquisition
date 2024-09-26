using DynamicPLCDataCollector.DataStorages;

var dataCollector = new DataCollector(
    (ipAddress, port) => new PLCClient(ipAddress, port), 
    metricTableConfig => new SQLiteDataStorage(metricTableConfig));

await dataCollector.StartCollectionTasks();

await Task.Delay(Timeout.Infinite);