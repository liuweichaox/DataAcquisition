using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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
///     本地 Parquet 文件存储服务（WAL 降级存储）
/// </summary>
public class ParquetFileStorageService : IDataStorageService, IDisposable
{
    private readonly string _directory;

    private readonly SemaphoreSlim _lock = new(1, 1);

    // 跟踪正在写入的文件，避免 GetPendingFilesAsync 返回不完整的文件
    private readonly ConcurrentHashSet<string> _writingFiles = new();

    public ParquetFileStorageService(IConfiguration configuration)
    {
        _directory = configuration["Parquet:Directory"] ?? "Data/parquet";

        Directory.CreateDirectory(_directory);
    }

    public async Task<bool> SaveBatchAsync(List<DataMessage>? dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return true;

        try
        {
            await SaveBatchInternalAsync(dataMessages, false).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Parquet 批量写入失败: {ex}");
            return false;
        }
    }

    public void Dispose()
    {
        _lock?.Dispose();
        _writingFiles.Clear();
    }

    /// <summary>
    ///     批量保存数据消息并返回文件路径（用于需要文件路径的场景，如 WAL）。
    ///     这是 SaveBatchAsync 的重载方法，返回文件路径而不是 bool。
    /// </summary>
    public async Task<string> SaveBatchAsync(List<DataMessage> dataMessages, bool returnFilePath)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            throw new ArgumentException("数据消息列表不能为空", nameof(dataMessages));

        return await SaveBatchInternalAsync(dataMessages, true).ConfigureAwait(false);
    }

    /// <summary>
    ///     内部方法：将一批消息写入一个新的 Parquet 文件，返回文件路径。
    /// </summary>
    private async Task<string> SaveBatchInternalAsync(List<DataMessage> dataMessages, bool validateFileSize)
    {
        var filePath = CreateNewFilePath(dataMessages);

        // 标记文件正在写入
        _writingFiles.Add(filePath);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var schema = GetSchema();

            // 写入文件
            await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                await using var parquetWriter =
                    await ParquetWriter.CreateAsync(schema, stream, append: false).ConfigureAwait(false);
                parquetWriter.CompressionMethod = CompressionMethod.Snappy;

                // 创建 row group 并写入数据
                using (var rowGroupWriter = parquetWriter.CreateRowGroup())
                {
                    await WriteColumnsAsync(rowGroupWriter, schema, dataMessages).ConfigureAwait(false);
                    // rowGroupWriter Dispose 时会写入 row group 数据
                }

                // 确保数据被刷新
                await stream.FlushAsync().ConfigureAwait(false);
            }

            // 验证文件大小（仅在需要时验证）
            if (validateFileSize)
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists || fileInfo.Length < 100)
                    throw new IOException($"Parquet 文件写入后大小异常: {fileInfo.Length} 字节，可能数据未正确写入");
            }

            return filePath;
        }
        catch (Exception ex)
        {
            // 如果写入失败，删除可能已创建的不完整文件
            if (File.Exists(filePath))
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // 忽略删除失败
                }

            if (validateFileSize) throw new IOException($"Parquet 文件写入失败: {ex.Message}", ex);
            throw;
        }
        finally
        {
            _lock.Release();
            // 写入完成，移除标记
            _writingFiles.TryRemove(filePath);
        }
    }

    /// <summary>
    ///     准备列数据并写入 Parquet 文件
    /// </summary>
    private static async Task WriteColumnsAsync(dynamic rowGroupWriter, ParquetSchema schema,
        List<DataMessage>? dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0) return;

        var rowCount = dataMessages.Count;

        // 准备列数据
        var timestamps = dataMessages.Select(x => x.Timestamp).ToArray();
        var measurements = dataMessages.Select(x => x.Measurement).ToArray();
        var plcCodes = dataMessages.Select(x => x.PLCCode ?? string.Empty).ToArray();
        var channelCodes = dataMessages.Select(x => x.ChannelCode ?? string.Empty).ToArray();
        var cycleIds = dataMessages.Select(x => x.CycleId ?? string.Empty).ToArray();
        var eventTypes = dataMessages.Select(x => x.EventType?.ToString() ?? string.Empty).ToArray();
        var dataJsons = dataMessages.Select(x =>
            JsonSerializer.Serialize((IDictionary<string, object?>)x.DataValues)).ToArray();

        // 验证所有数组长度一致
        if (timestamps.Length != rowCount || measurements.Length != rowCount ||
            plcCodes.Length != rowCount || channelCodes.Length != rowCount ||
            cycleIds.Length != rowCount || eventTypes.Length != rowCount ||
            dataJsons.Length != rowCount)
            throw new InvalidOperationException(
                $"数据列长度不一致: rowCount={rowCount}, timestamps={timestamps.Length}, measurements={measurements.Length}");

        // 按顺序写入所有列
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DateTimeDataField)schema.DataFields[0], timestamps))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)schema.DataFields[1], measurements))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)schema.DataFields[2], plcCodes))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)schema.DataFields[3], channelCodes))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)schema.DataFields[4], cycleIds))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)schema.DataFields[5], eventTypes))
            .ConfigureAwait(false);
        await rowGroupWriter.WriteColumnAsync(new DataColumn((DataField<string>)schema.DataFields[6], dataJsons))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     获取所有待上传的 Parquet 文件（排除正在写入的文件）
    /// </summary>
    public Task<List<string>> GetPendingFilesAsync()
    {
        var files = Directory.Exists(_directory)
            ? Directory.GetFiles(_directory, "*.parquet", SearchOption.TopDirectoryOnly).ToList()
            : new List<string>();

        // 排除正在写入的文件，避免读取不完整的文件
        if (_writingFiles.Count > 0) files.RemoveAll(f => _writingFiles.Contains(f));

        return Task.FromResult(files);
    }

    /// <summary>
    ///     读取 Parquet 文件并转换为 DataMessage 列表
    /// </summary>
    public async Task<List<DataMessage>> ReadFileAsync(string filePath)
    {
        var messages = new List<DataMessage>();

        try
        {
            // 检查文件是否存在和大小
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length < 100) return messages;

            using var stream = File.OpenRead(filePath);
            using var reader = await ParquetReader.CreateAsync(stream).ConfigureAwait(false);
            var schema = reader.Schema;

            // 检查是否有 row group
            if (reader.RowGroupCount == 0) return messages;

            // 读取所有 row groups
            for (var i = 0; i < reader.RowGroupCount; i++)
            {
                using var rowGroupReader = reader.OpenRowGroupReader(i);
                var tsColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[0]).ConfigureAwait(false);
                var msColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[1]).ConfigureAwait(false);
                var plcColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[2]).ConfigureAwait(false);
                var channelColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[3]).ConfigureAwait(false);
                var ciColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[4]).ConfigureAwait(false);
                var etColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[5]).ConfigureAwait(false);
                var jsonColumn = await rowGroupReader.ReadColumnAsync(schema.DataFields[6]).ConfigureAwait(false);

                var timestamps = tsColumn.Data.Cast<DateTime>().ToArray();
                var measurements = msColumn.Data.Cast<string>().ToArray();
                var plcCodes = plcColumn.Data.Cast<string>().ToArray();
                var channelCodes = channelColumn.Data.Cast<string>().ToArray();
                var cycleIds = ciColumn.Data.Cast<string>().ToArray();
                var eventTypes = etColumn.Data.Cast<string>().ToArray();
                var dataJsons = jsonColumn.Data.Cast<string>().ToArray();

                for (var row = 0; row < timestamps.Length; row++)
                {
                    var msg = DataMessage.Create(
                        cycleIds[row],
                        measurements[row],
                        plcCodes[row],
                        channelCodes[row],
                        Enum.Parse<EventType>(eventTypes[row]),
                        timestamps[row]);

                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(dataJsons[row]);
                    if (dict != null)
                        foreach (var kv in dict)
                            msg.AddDataValue(kv.Key, kv.Value);

                    messages.Add(msg);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Parquet 文件读取失败 {filePath}: {ex}");
            return messages;
        }

        return messages;
    }

    /// <summary>
    ///     删除指定的 Parquet 文件
    /// </summary>
    public Task DeleteFileAsync(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     创建新的文件路径
    /// </summary>
    private string CreateNewFilePath(List<DataMessage> dataMessages)
    {
        var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
        var fileName = $"{measurement}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.parquet";
        return Path.Combine(_directory, fileName);
    }

    /// <summary>
    ///     获取 Parquet Schema 定义
    /// </summary>
    private static ParquetSchema GetSchema()
    {
        return new ParquetSchema(
            new DateTimeDataField("timestamp", DateTimeFormat.DateAndTime),
            new DataField<string>("measurement"),
            new DataField<string>("plc_code"),
            new DataField<string>("channel_code"),
            new DataField<string>("cycle_id"),
            new DataField<string>("event_type"),
            new DataField<string>("data_json")
        );
    }

    /// <summary>
    ///     线程安全的 HashSet 实现
    /// </summary>
    private class ConcurrentHashSet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _dictionary = new();

        public int Count => _dictionary.Count;

        public bool Add(T item)
        {
            return _dictionary.TryAdd(item, 0);
        }

        public bool TryRemove(T item)
        {
            return _dictionary.TryRemove(item, out _);
        }

        public bool Contains(T item)
        {
            return _dictionary.ContainsKey(item);
        }

        public void Clear()
        {
            _dictionary.Clear();
        }
    }
}