using DataAcquisition.Core.DataAcquisitions;

namespace DataAcquisition.Gateway;

public class DataAcquisitionHostedService(IDataAcquisitionService dataAcquisitionService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await dataAcquisitionService.StartCollectionTasks();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await dataAcquisitionService.StopCollectionTasks();
    }
}