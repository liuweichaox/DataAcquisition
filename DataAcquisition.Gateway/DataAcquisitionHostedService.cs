using DataAcquisition.Core.DataAcquisitions;
using Microsoft.Extensions.Hosting;

namespace DataAcquisition.Gateway;

public class DataAcquisitionHostedService : BackgroundService
{
    private readonly IDataAcquisitionService _dataAcquisitionService;

    public DataAcquisitionHostedService(IDataAcquisitionService dataAcquisitionService)
    {
        _dataAcquisitionService = dataAcquisitionService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动采集任务
        await _dataAcquisitionService.StartCollectionTasks();

        // 等待停止信号
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // 服务停止时会触发取消
        }

        // 收尾：停止采集任务
        await _dataAcquisitionService.StopCollectionTasks();
    }
}