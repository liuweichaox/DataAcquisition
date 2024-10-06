using DataAcquisition.Models;
using DataAcquisition.Services.DataAcquisitions;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.PLCClients;
using Samples.Services.DataAcquisitionConfigs;
using Samples.Services.DataStorages;
using Samples.Services.Devices;
using Samples.Services.PLCClients;

var deviceService = new DeviceService();

var dataAcquisitionConfigService = new DataAcquisitionConfigService();

var dataAcquisitionService = new DataAcquisitionService(deviceService, dataAcquisitionConfigService, PLCClientFactory, DataStorageFactory, ProcessReadData);

await dataAcquisitionService.StartCollectionTasks();

IPLCClient PLCClientFactory(string ipAddress, int port) => new PLCClient(ipAddress, port);

IDataStorage DataStorageFactory(Device device, DataAcquisitionConfig metricTableConfig) => new SQLiteDataStorage(device, metricTableConfig);

void ProcessReadData(Dictionary<string, object> data, Device device)
{
    data["时间"] = DateTime.Now;
    data["DeviceCode"] = device.Code;
}