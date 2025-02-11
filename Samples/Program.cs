using DataAcquisition.Models;
using DataAcquisition.Services.DataAcquisitions;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.PLCClients;
using Samples.Services.DataAcquisitionConfigs;
using Samples.Services.DataStorages;
using Samples.Services.PLCClients;

var dataAcquisitionConfigService = new DataAcquisitionConfigService();

var dataAcquisitionService = new DataAcquisitionService(
    dataAcquisitionConfigService,
    PLCClientFactory,
    DataStorageFactory,
    ProcessReadData);

await dataAcquisitionService.StartCollectionTasks();

IPLCClient PLCClientFactory(string ipAddress, int port)
    => new PLCClient(ipAddress, port);

IDataStorage DataStorageFactory(DataAcquisitionConfig metricTableConfig)
    => new SQLiteDataStorage(metricTableConfig);

void ProcessReadData(Dictionary<string, object> data, DataAcquisitionConfig config)
{
    data["时间"] = DateTime.Now;
    data["DeviceCode"] = config.Code;
}