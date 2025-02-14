using DataAcquisition.Services.DataAcquisitions;
using Samples.Services.DataAcquisitionConfigs;
using Samples.Services.DataStorages;
using Samples.Services.PLCClients;
using Samples.Services.QueueManagers;

var dataAcquisitionConfigService = new DataAcquisitionConfigService();

var dataAcquisitionService = new DataAcquisitionService(
    dataAcquisitionConfigService,
    (ipAddress, port) => new PlcClient(ipAddress, port),
    config => new SQLiteDataStorage(config),
    (factory, config) => new QueueManager(factory, config),
    (data, config) =>
    {
        data["时间"] = DateTime.Now;
        data["DeviceCode"] = config.Code;
    });

await dataAcquisitionService.StartCollectionTasks();