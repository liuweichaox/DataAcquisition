using DataAcquisition.Models;
using DataAcquisition.Services.DataAcquisitions;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.Devices;
using DataAcquisition.Services.MetricTableConfigs;
using DataAcquisition.Services.PLCClients;
using Samples.Services.DataStorages;
using Samples.Services.Devices;
using Samples.Services.MetricTableConfigs;
using Samples.Services.PLCClients;

IDeviceService deviceService = new DeviceService();

IMetricTableConfigService metricTableConfigService = new MetricTableConfigService();

var dataAcquisitionService = new DataAcquisitionService(deviceService, metricTableConfigService, PLCClientFactory, DataStorageFactory, ProcessReadData);

await dataAcquisitionService.StartCollectionTasks();

IPLCClient PLCClientFactory(string ipAddress, int port) => new PLCClient(ipAddress, port);


IDataStorage DataStorageFactory(Device device, MetricTableConfig metricTableConfig) => new SQLiteDataStorage(device, metricTableConfig);

void ProcessReadData(Dictionary<string, object> data, Device device)
{
    data["时间"] = DateTime.Now;
    data["DeviceCode"] = device.Code;
}