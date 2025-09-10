using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Gateway.BackgroundServices;

/// <summary>
/// 后台服务，用于管理数据采集任务的生命周期。
/// </summary>
public class DataAcquisitionHostedService : BackgroundService
{
    private readonly IDataAcquisitionService _dataAcquisitionService;

    public DataAcquisitionHostedService(IDataAcquisitionService dataAcquisitionService)
    {
        _dataAcquisitionService = dataAcquisitionService;
    }

    /// <summary>
    /// 执行数据采集后台任务。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start data acquisition tasks.
        await _dataAcquisitionService.StartCollectionTasks();

        // Wait for a cancellation signal.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Triggered when the service stops.
        }

        // Stop data acquisition tasks.
        await _dataAcquisitionService.StopCollectionTasks();
    }
}