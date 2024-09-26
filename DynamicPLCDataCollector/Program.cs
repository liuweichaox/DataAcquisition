using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.PLCClients;

IPLCClientManager clientManager = new PLCClientManager();

IDataStorage dataStorage = new SQLiteDataStorage();

var dataCollector = new DataCollector(clientManager, dataStorage);

await dataCollector.StartCollectionTasks();

await Task.Delay(Timeout.Infinite);