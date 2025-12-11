using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Infrastructure.DataStorages;
using Microsoft.Extensions.Hosting;

namespace DataAcquisition.Gateway.BackgroundServices;

/// <summary>
/// 后台服务：扫描 Parquet 降级文件，批量写回 InfluxDB，成功后删除。
/// </summary>
public class ParquetRetryWorker : BackgroundService
{
    private readonly ParquetFileStorageService _parquetStorage;
    private readonly InfluxDbDataStorageService _influxStorage;
    private readonly IOperationalEventsService _events;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 500;

    public ParquetRetryWorker(
        ParquetFileStorageService parquetStorage,
        InfluxDbDataStorageService influxStorage,
        IOperationalEventsService events)
    {
        _parquetStorage = parquetStorage;
        _influxStorage = influxStorage;
        _events = events;
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
                await _events.ErrorAsync($"Parquet 重传任务异常: {ex.Message}", ex).ConfigureAwait(false);
            }

            await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingFilesAsync()
    {
        var files = await _parquetStorage.GetPendingFilesAsync().ConfigureAwait(false);
        if (files.Count == 0) return;

        await _events.InfoAsync($"发现 {files.Count} 个待上传的 Parquet 文件").ConfigureAwait(false);

        foreach (var file in files)
        {
            try
            {
                var messages = await _parquetStorage.ReadFileAsync(file).ConfigureAwait(false);
                if (messages.Count == 0)
                {
                    await _parquetStorage.DeleteFileAsync(file).ConfigureAwait(false);
                    continue;
                }

                // 分批写入 InfluxDB
                var offset = 0;
                while (offset < messages.Count)
                {
                    var batch = messages.Skip(offset).Take(BatchSize).ToList();
                    await _influxStorage.SaveBatchAsync(batch).ConfigureAwait(false);
                    offset += batch.Count;
                }

                await _parquetStorage.DeleteFileAsync(file).ConfigureAwait(false);
                await _events.InfoAsync($"成功写回并删除文件: {file}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _events.WarnAsync($"处理 Parquet 文件失败，保留文件以便下次重试: {file}, 原因: {ex.Message}").ConfigureAwait(false);
            }
        }
    }
}
