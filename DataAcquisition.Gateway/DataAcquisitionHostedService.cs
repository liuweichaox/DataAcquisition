using DataAcquisition.Core.DataAcquisitions;

namespace DataAcquisition.Gateway;

public class DataAcquisitionHostedService : IHostedService
{
    private readonly IDataAcquisition _dataAcquisition;

    public DataAcquisitionHostedService(IDataAcquisition dataAcquisition)
    {
        _dataAcquisition = dataAcquisition;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _dataAcquisition.StartCollectionTasks();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _dataAcquisition.StopCollectionTasks();
    }
}
