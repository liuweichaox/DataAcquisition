using DataAcquisition.Services.DataAcquisitions;
using Samples.Services.DataAcquisitionConfigs;
using Samples.Services.DataStorages;
using Samples.Services.PLCClients;

var dataAcquisitionConfigService = new DataAcquisitionConfigService();

var dataAcquisitionService = new DataAcquisitionService(
    dataAcquisitionConfigService,
    (ipAddress, port) => new PlcClient(ipAddress, port),
    config => new SQLiteDataStorage(config),
    (data, config) =>
    {
        data["时间"] = DateTime.Now;
        data["DeviceCode"] = config.Code;
    });

await dataAcquisitionService.StartCollectionTasks();