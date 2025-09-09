using DataAcquisition.Core.DataAcquisitions;
using Microsoft.Extensions.Hosting;

namespace DataAcquisition.Gateway;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动数据采集任务。
        await _dataAcquisitionService.StartCollectionTasks();

        // 等待取消信号。
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // 服务停止时触发取消。
        }

        // 停止数据采集任务。
        await _dataAcquisitionService.StopCollectionTasks();
    }
}