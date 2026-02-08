using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace DataAcquisition.Infrastructure.DataStorages;

/// <summary>
///     基于 Parquet 文件的 WAL 存储服务。pending/ 存放新写入的 WAL，retry/ 存放待重试的文件。
/// </summary>
public class ParquetFileStorageService : IWalStorageService, IDataStorageService, IDisposable
{
    private readonly string _pendingDirectory;
    private readonly string _retryDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentHashSet<string> _writingFiles = new();
    private readonly ILogger<ParquetFileStorageService> _logger;

    public ParquetFileStorageService(IConfiguration configuration, ILogger<ParquetFileStorageService> logger)
    {
        _logger = logger;
        var baseDirectory = configuration["Parquet:Directory"] ?? "Data/parquet";
        _pendingDirectory = Path.Combine(baseDirectory, "pending");
        _retryDirectory = Path.Combine(baseDirectory, "retry");
        Directory.CreateDirectory(_pendingDirectory);
        Directory.CreateDirectory(_retryDirectory);
    }

    // ─── IDataStorageService ───────────────────────

    public async Task<bool> SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages.Count == 0) return true;
        try
        {
            await WriteInternalAsync(dataMessages, validateSize: false).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parquet 批量写入失败");
            return false;
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
        _writingFiles.Clear();
    }

    // ─── IWalStorageService ────────────────────────

    public async Task<string> WriteAsync(List<DataMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0) throw new ArgumentException("数据消息列表不能为空", nameof(messages));
        return await WriteInternalAsync(messages, validateSize: true).ConfigureAwait(false);
    }

    public Task<List<DataMessage>> ReadAsync(string filePath) => ReadFileInternalAsync(filePath);

    public Task DeleteAsync(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }

    public Task<List<string>> GetRetryFilesAsync()
    {
        var files = Directory.Exists(_retryDirectory)
            ? Directory.GetFiles(_retryDirectory, "*.parquet", SearchOption.TopDirectoryOnly)
                .Where(f => !_writingFiles.Contains(f)).ToList()
            : [];
        return Task.FromResult(files);
    }

    // ─── 内部实现 ──────────────────────────────────

    private async Task<string> WriteInternalAsync(List<DataMessage> dataMessages, bool validateSize)
    {
        var filePath = CreateNewFilePath(dataMessages, _pendingDirectory);
        _writingFiles.Add(filePath);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var schema = GetSchema();
            await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                await using var writer = await ParquetWriter.CreateAsync(schema, stream, append: false).ConfigureAwait(false);
                writer.CompressionMethod = CompressionMethod.Snappy;
                using var rowGroup = writer.CreateRowGroup();
                await WriteColumnsAsync(rowGroup, schema, dataMessages).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            if (validateSize)
            {
                var fi = new FileInfo(filePath);
                if (!fi.Exists || fi.Length < 100)
                    throw new IOException($"Parquet 文件大小异常: {fi.Length} 字节");
            }
            return filePath;
        }
        catch
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch { /* ignore */ }
            throw;
        }
        finally
        {
            _lock.Release();
            _writingFiles.TryRemove(filePath);
        }
    }

    private static async Task WriteColumnsAsync(dynamic rowGroupWriter, ParquetSchema schema,
        List<DataMessage> dataMessages)
    {
        if (dataMessages is not { Count: > 0 }) return;

        var timestamps = dataMessages.Select(x => x.Timestamp).ToArray();
        var measurements = dataMessages.Select(x => x.Measurement).ToArray();
        var plcCodes = dataMessages.Select(x => x.PlcCode).ToArray();
        var channelCodes = dataMessages.Select(x => x.ChannelCode).ToArray();
        var cycleIds = dataMessages.Select(x => x.CycleId).ToArray();
        var eventTypes = dataMessages.Select(x => x.EventType.ToString()).ToArray();
        var dataJsons = dataMessages.Select(x =>
            JsonSerializer.Serialize((IDictionary<string, object?>)x.DataValues)).ToArray();

        for (var i = 0; i < schema.DataFields.Length; i++)
        {
            object[] arrays = [timestamps, measurements, plcCodes, channelCodes, cycleIds, eventTypes, dataJsons];
            var field = schema.DataFields[i];
            var column = i == 0
                ? new DataColumn((DateTimeDataField)field, (DateTime[])arrays[i])
                : new DataColumn((DataField<string>)field, (string[])arrays[i]);
            await rowGroupWriter.WriteColumnAsync(column).ConfigureAwait(false);
        }
    }

    private async Task<List<DataMessage>> ReadFileInternalAsync(string filePath)
    {
        var messages = new List<DataMessage>();
        try
        {
            var fi = new FileInfo(filePath);
            if (!fi.Exists || fi.Length < 100) return messages;

            await using var stream = File.OpenRead(filePath);
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
            _logger.LogWarning(ex, "Parquet 文件读取失败: {FilePath}", filePath);
            return messages;
        }

        return messages;
    }

    public Task MoveToRetryAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return Task.CompletedTask;

        try
        {
            var fileName = Path.GetFileName(filePath);
            var retryPath = Path.Combine(_retryDirectory, fileName);

            // 如果 retry 文件夹中已存在同名文件，先删除（理论上不应该发生）
            if (File.Exists(retryPath))
                File.Delete(retryPath);

            // 移动文件到 retry 文件夹（原子操作）
            File.Move(filePath, retryPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "移动文件到 retry 失败: {FilePath}", filePath);
        }

        return Task.CompletedTask;
    }

    private static string CreateNewFilePath(List<DataMessage> dataMessages, string directory)
    {
        var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
        return Path.Combine(directory, $"{measurement}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.parquet");
    }

    private static ParquetSchema GetSchema() => new(
        new DateTimeDataField("timestamp", DateTimeFormat.DateAndTime),
        new DataField<string>("measurement"),
        new DataField<string>("plc_code"),
        new DataField<string>("channel_code"),
        new DataField<string>("cycle_id"),
        new DataField<string>("event_type"),
        new DataField<string>("data_json"));

    /// <summary>线程安全的 HashSet。</summary>
    private class ConcurrentHashSet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _dictionary = new();
        public bool Add(T item) => _dictionary.TryAdd(item, 0);
        public bool TryRemove(T item) => _dictionary.TryRemove(item, out _);
        public bool Contains(T item) => _dictionary.ContainsKey(item);
        public void Clear() => _dictionary.Clear();
    }
}