using DataAcquisition.Services.DataAcquisitions;
using Samples.Services.DataAcquisitionConfigs;
using Samples.Services.DataStorages;
using Samples.Services.PLCClients;
using Samples.Services.QueueManagers;

var dataAcquisitionConfigService = new DataAcquisitionConfigService();

var dataAcquisitionService = new DataAcquisitionService(
    dataAcquisitionConfigService,
    (ipAddress, port) => new PlcClient(ipAddress, port),
    config => new SqLiteDataStorage(config),
    (factory, config) => new QueueManager(factory, config),
    (data, config) =>
    {
        data.Values["Timestamp"] = DateTime.Now;
    });

await dataAcquisitionService.StartCollectionTasks();