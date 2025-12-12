using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Configuration;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace DataAcquisition.Infrastructure.DataStorages;

/// <summary>
/// 本地 Parquet 追加存储（降级存储），支持文件滚动。
/// </summary>
public class ParquetFileStorageService : IDataStorageService, IDisposable
{
    private readonly string _directory;
    private readonly long _maxFileSizeBytes;
    private readonly TimeSpan _maxFileAge;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string _currentFilePath = string.Empty;
    private DateTime _currentFileCreatedAt = DateTime.Now;

    public ParquetFileStorageService(IConfiguration configuration)
    {
        _directory = configuration["Parquet:Directory"] ?? "Data/parquet";
        _maxFileSizeBytes = TryParseSize(configuration["Parquet:MaxFileSize"], 50 * 1024 * 1024); // 默认 50MB
        _maxFileAge = TimeSpan.TryParse(configuration["Parquet:MaxFileAge"], out var age)
            ? age
            : TimeSpan.FromMinutes(5);

        Directory.CreateDirectory(_directory);
    }

    public Task<bool> SaveAsync(DataMessage dataMessage) => SaveBatchAsync(new List<DataMessage> { dataMessage });

    public async Task<bool> SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return true;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await EnsureCurrentFileAsync().ConfigureAwait(false);

            var schema = GetSchema();
            using var stream = new FileStream(_currentFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            using var parquetWriter = await ParquetWriter.CreateAsync(schema, stream, append: true).ConfigureAwait(false);
            using var rowGroupWriter = parquetWriter.CreateRowGroup();

            // 准备列数据并写入
            await WriteColumnsAsync(rowGroupWriter, schema, dataMessages).ConfigureAwait(false);

            // 滚动判断
            await RollIfNeededAsync().ConfigureAwait(false);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 准备列数据并写入 Parquet 文件
    /// </summary>
    private static async Task WriteColumnsAsync(dynamic rowGroupWriter, ParquetSchema schema, List<DataMessage> dataMessages)
    {
        // 保持本地时间，不再转换为 UTC
        var timestamps = dataMessages.Select(x => x.Timestamp).ToArray();
        var batchSizes = dataMessages.Select(x => x.BatchSize).ToArray();
        var measurements = dataMessages.Select(x => x.Measurement ?? string.Empty).ToArray();
        var plcCodes = dataMessages.Select(x => x.PLCCode ?? string.Empty).ToArray();
        var channelCodes = dataMessages.Select(x => x.ChannelCode ?? string.Empty).ToArray();
        var cycleIds = dataMessages.Select(x => x.CycleId ?? string.Empty).ToArray();
        var eventTypes = dataMessages.Select(x => x.EventType?.ToString() ?? string.Empty).ToArray();
        var dataJsons = dataMessages.Select(x =>
            System.Text.Json.JsonSerializer.Serialize((IDictionary<string, object?>)x.DataValues)).ToArray();

        await rowGroupWriter.WriteColumnAsync(new DataColumn((DateTimeDataField)schema.DataFields[0], timestamps)).ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField)schema.DataFields[1], batchSizes)).ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField)schema.DataFields[2], measurements)).ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField)schema.DataFields[3], plcCodes)).ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField)schema.DataFields[4], channelCodes)).ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField)schema.DataFields[5], cycleIds)).ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField)schema.DataFields[6], eventTypes)).ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField)schema.DataFields[7], dataJsons)).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取所有待上传的 Parquet 文件（排除当前正在写入的文件）
    /// </summary>
    public Task<List<string>> GetPendingFilesAsync()
    {
        var files = Directory.Exists(_directory)
            ? Directory.GetFiles(_directory, "*.parquet", SearchOption.TopDirectoryOnly).ToList()
            : new List<string>();

        // 排除当前文件
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            files.RemoveAll(f => string.Equals(f, _currentFilePath, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(files);
    }

    /// <summary>
    /// 读取 Parquet 文件并转换为 DataMessage 列表
    /// </summary>
    public async Task<List<DataMessage>> ReadFileAsync(string filePath)
    {
        var messages = new List<DataMessage>();

        // 检查文件是否存在和大小（Parquet 文件至少需要一定的字节数才能有效）
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.Length < 100) // Parquet 文件最小有效大小约为 100 字节
        {
            // 文件太小，可能是损坏的文件，返回空列表
            return messages;
        }

        using var stream = File.OpenRead(filePath);
        using var reader = await ParquetReader.CreateAsync(stream).ConfigureAwait(false);
        var schema = reader.Schema;

        for (int i = 0; i < reader.RowGroupCount; i++)
        {
            using var rowGroupReader = reader.OpenRowGroupReader(i);
            var tsColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[0]).ConfigureAwait(false);
            var bsColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[1]).ConfigureAwait(false);
            var msColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[2]).ConfigureAwait(false);
            var plcColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[3]).ConfigureAwait(false);
            var channelColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[4]).ConfigureAwait(false);
            var ciColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[5]).ConfigureAwait(false);
            var etColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[6]).ConfigureAwait(false);
            var jsonColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[7]).ConfigureAwait(false);

            var timestamps = tsColumn.Data.Cast<DateTime>().ToArray();
            var batchSizes = bsColumn.Data.Cast<int>().ToArray();
            var measurements = msColumn.Data.Cast<string>().ToArray();
            var plcCodes = plcColumn.Data.Cast<string>().ToArray();
            var channelCodes = channelColumn.Data.Cast<string>().ToArray();
            var cycleIds = ciColumn.Data.Cast<string>().ToArray();
            var eventTypes = etColumn.Data.Cast<string>().ToArray();
            var dataJsons = jsonColumn.Data.Cast<string>().ToArray();

            for (int row = 0; row < timestamps.Length; row++)
            {
                var batchSize = (batchSizes != null && batchSizes.Length > row) ? batchSizes[row] : 1;
                var msg = DataMessage.Create(cycleIds[row], measurements[row], plcCodes[row], channelCodes[row], Enum.Parse<EventType>(eventTypes[row].ToString()), timestamps[row], batchSize);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(dataJsons[row]);
                if (dict != null)
                {
                    foreach (var kv in dict)
                    {
                        msg.AddDataValue(kv.Key, kv.Value);
                    }
                }

                messages.Add(msg);
            }
        }

        return messages;
    }

    public Task DeleteFileAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 将一批消息写入一个新的 Parquet 文件，返回文件路径。
    /// </summary>
    public async Task<string> SaveBatchAsNewFileAsync(List<DataMessage> dataMessages)
    {
        var filePath = CreateNewFilePath();
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _currentFilePath = filePath;
            _currentFileCreatedAt = DateTime.Now;

            var schema = GetSchema();
            using var stream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var parquetWriter = await ParquetWriter.CreateAsync(schema, stream, append: false).ConfigureAwait(false);
            using var rowGroupWriter = parquetWriter.CreateRowGroup();

            await WriteColumnsAsync(rowGroupWriter, schema, dataMessages).ConfigureAwait(false);

            return _currentFilePath;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureCurrentFileAsync()
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath))
        {
            _currentFilePath = CreateNewFilePath();
            _currentFileCreatedAt = DateTime.Now;
            using var stream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var schema = GetSchema();
            using var writer = await ParquetWriter.CreateAsync(schema, stream).ConfigureAwait(false);
            writer.CompressionMethod = CompressionMethod.Snappy;
            using var rowGroupWriter = writer.CreateRowGroup(); // 创建空文件
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    private async Task RollIfNeededAsync()
    {
        var info = new FileInfo(_currentFilePath);
        var age = DateTime.Now - _currentFileCreatedAt;
        if (info.Length >= _maxFileSizeBytes || age >= _maxFileAge)
        {
            _currentFilePath = CreateNewFilePath();
            _currentFileCreatedAt = DateTime.Now;
            using var stream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var schema = GetSchema();
            using var writer = await ParquetWriter.CreateAsync(schema, stream).ConfigureAwait(false);
            writer.CompressionMethod = CompressionMethod.Snappy;
            using var rowGroupWriter = writer.CreateRowGroup();
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    private string CreateNewFilePath()
    {
        var fileName = $"data_{DateTime.Now:yyyyMMdd_HHmmss_fff}.parquet";
        return Path.Combine(_directory, fileName);
    }

    private static ParquetSchema GetSchema()
    {
        return new ParquetSchema(
            new DateTimeDataField("timestamp", DateTimeFormat.DateAndTime, true),
            new DataField<int>("batch_size"),
            new DataField<string>("measurement"),
            new DataField<string>("plc_code"),
            new DataField<string>("channel_code"),
            new DataField<string>("cycle_id"),
            new DataField<string>("event_type"),
            new DataField<string>("data_json")
        );
    }

    private static long TryParseSize(string? value, long defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        // 支持如 "50MB"、"10M"、"1GB"
        value = value.Trim().ToUpperInvariant();
        long multiplier = 1;
        if (value.EndsWith("KB"))
        {
            multiplier = 1024;
            value = value[..^2];
        }
        else if (value.EndsWith("MB"))
        {
            multiplier = 1024 * 1024;
            value = value[..^2];
        }
        else if (value.EndsWith("GB"))
        {
            multiplier = 1024L * 1024 * 1024;
            value = value[..^2];
        }
        else if (value.EndsWith("M"))
        {
            multiplier = 1024 * 1024;
            value = value[..^1];
        }

        return long.TryParse(value, out var number) ? number * multiplier : defaultValue;
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
