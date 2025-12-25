using DataAcquisition.Infrastructure.DataStorages;

namespace DataAcquisition.Worker.BackgroundServices;

/// <summary>
///     后台服务：扫描 Parquet 降级文件，批量写回 InfluxDB，成功后删除。
/// </summary>
public class ParquetRetryWorker : BackgroundService
{
    private readonly InfluxDbDataStorageService _influxStorage;

    // 缩短扫描间隔，加快 WAL → Influx 写入延迟
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private readonly ILogger<ParquetRetryWorker> _logger;
    private readonly ParquetFileStorageService _parquetStorage;

    public ParquetRetryWorker(
        ParquetFileStorageService parquetStorage,
        InfluxDbDataStorageService influxStorage,
        ILogger<ParquetRetryWorker> logger)
    {
        _parquetStorage = parquetStorage;
        _influxStorage = influxStorage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false); // 启动延迟

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingFilesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Parquet 重传任务异常: {Message}", ex.Message);
            }

            await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingFilesAsync()
    {
        var files = await _parquetStorage.GetPendingFilesAsync().ConfigureAwait(false);
        if (files.Count == 0) return;

        _logger.LogInformation("发现 {Count} 个待上传的 Parquet 文件", files.Count);

        foreach (var file in files)
            try
            {
                var messages = await _parquetStorage.ReadFileAsync(file).ConfigureAwait(false);
                if (messages.Count == 0)
                {
                    // 文件为空或损坏，删除它
                    _logger.LogWarning("Parquet 文件为空或损坏，删除: {File}", file);
                    await _parquetStorage.DeleteFileAsync(file).ConfigureAwait(false);
                    continue;
                }

                // 由于每个文件只包含一个 BatchSize 的消息（写入时就是按 BatchSize 一个文件），
                // 所以文件中的所有消息都是同一个批次的，可以直接一次性写入 InfluxDB
                var success = await _influxStorage.SaveBatchAsync(messages).ConfigureAwait(false);

                // 只有在写入成功时才删除 Parquet 文件
                if (success)
                {
                    await _parquetStorage.DeleteFileAsync(file).ConfigureAwait(false);
                    _logger.LogInformation("成功写回并删除文件: {File} (包含 {Count} 条消息)", file, messages.Count);
                }
                else
                {
                    _logger.LogWarning("写入 InfluxDB 失败，保留文件以便下次重试: {File} (包含 {Count} 条消息)", file, messages.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "处理 Parquet 文件失败，保留文件以便下次重试: {File}, 原因: {Message}", file, ex.Message);
            }
    }
}