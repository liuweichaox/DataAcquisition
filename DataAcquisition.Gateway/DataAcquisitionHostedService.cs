using System.Collections.Concurrent;
using DataAcquisition.Core.DataAcquisitions;
using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Models;

namespace DataAcquisition.Gateway;

public class DataAcquisitionHostedService : IHostedService
{
    private readonly IDataAcquisitionService _dataAcquisitionService;

    public DataAcquisitionHostedService(IDataAcquisitionService dataAcquisitionService)
    {
        _dataAcquisitionService = dataAcquisitionService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _dataAcquisitionService.StartCollectionTasks();
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _dataAcquisitionService.StopCollectionTasks();
    }
}
