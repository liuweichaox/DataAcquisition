using DynamicPLCDataCollector;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.Services.DataStorages;
using DynamicPLCDataCollector.Services.Devices;
using DynamicPLCDataCollector.Services.MetricTableConfigs;
using DynamicPLCDataCollector.Services.PLCClients;
using Samples.Services.Devices;
using Samples.Services.MetricTableConfigs;
using Samples.Services.PLCClients;

IDeviceService deviceService = new DeviceService();

IMetricTableConfigService metricTableConfigService = new MetricTableConfigService();

var dataCollector = new DataCollector(deviceService, metricTableConfigService, PLCClientFactory, DataStorageFactory, ProcessReadData);

await dataCollector.StartCollectionTasks();

IPLCClient PLCClientFactory(string ipAddress, int port) => new PLCClient(ipAddress, port);

IDataStorage DataStorageFactory(Device device, MetricTableConfig metricTableConfig) => new SQLiteDataStorage(device, metricTableConfig);

void ProcessReadData(Dictionary<string, object> data, Device device)
{
    data["时间"] = DateTime.Now;
    data["DeviceCode"] = device.Code;
}