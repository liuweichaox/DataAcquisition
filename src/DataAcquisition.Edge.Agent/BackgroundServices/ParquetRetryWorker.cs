using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Edge.Agent.BackgroundServices;

/// <summary>
///     后台服务：扫描 WAL 重试队列，写回主存储，成功后删除。
/// </summary>
public class ParquetRetryWorker(
    IWalStorageService walStorage,
    IDataStorageService primaryStorage,
    ILogger<ParquetRetryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRetryFilesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "WAL 重传任务异常");
            }
            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessRetryFilesAsync()
    {
        var files = await walStorage.GetRetryFilesAsync().ConfigureAwait(false);
        if (files.Count == 0) return;

        logger.LogInformation("发现 {Count} 个待重试的 WAL 文件", files.Count);

        foreach (var file in files)
        {
            try
            {
                var messages = await walStorage.ReadAsync(file).ConfigureAwait(false);
                if (messages.Count == 0)
                {
                    logger.LogWarning("WAL 文件为空或损坏，删除: {File}", file);
                    await walStorage.DeleteAsync(file).ConfigureAwait(false);
                    continue;
                }

                if (await primaryStorage.SaveBatchAsync(messages).ConfigureAwait(false))
                {
                    await walStorage.DeleteAsync(file).ConfigureAwait(false);
                    logger.LogInformation("重试成功: {File} ({Count} 条)", file, messages.Count);
                }
                else
                {
                    logger.LogWarning("主存储写入失败，保留待重试: {File}", file);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "处理 WAL 文件失败: {File}", file);
            }
        }
    }
}