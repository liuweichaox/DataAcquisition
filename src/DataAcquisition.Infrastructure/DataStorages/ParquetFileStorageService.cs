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

namespace DataAcquisition.Infrastructure.DataStorages;

/// <summary>
///     基于 Parquet 文件的 WAL 存储服务。pending/ 存放新写入的 WAL，retry/ 存放待重试的文件，
///     invalid/ 存放无法写入 WAL 的坏消息审计记录。
/// </summary>
public class ParquetFileStorageService : IWalStorageService, IDataStorageService, IDisposable
{
    private readonly string _invalidDirectory;
    private readonly string _pendingDirectory;
    private readonly string _retryDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _writingFiles = new();
    private readonly ILogger<ParquetFileStorageService> _logger;

    public ParquetFileStorageService(IConfiguration configuration, ILogger<ParquetFileStorageService> logger)
    {
        _logger = logger;
        var baseDirectory = configuration["Parquet:Directory"] ?? "Data/parquet";
        _pendingDirectory = Path.Combine(baseDirectory, "pending");
        _retryDirectory = Path.Combine(baseDirectory, "retry");
        _invalidDirectory = Path.Combine(baseDirectory, "invalid");
        Directory.CreateDirectory(_pendingDirectory);
        Directory.CreateDirectory(_retryDirectory);
        Directory.CreateDirectory(_invalidDirectory);
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
                .Where(f => !_writingFiles.ContainsKey(f)).ToList()
            : [];
        return Task.FromResult(files);
    }

    public async Task QuarantineInvalidAsync(DataMessage message, string reason)
    {
        ArgumentNullException.ThrowIfNull(message);

        var filePath = Path.Combine(_invalidDirectory, $"invalid_{DateTime.UtcNow:yyyyMMdd}.jsonl");
        var record = new
        {
            timestampUtc = DateTime.UtcNow,
            reason,
            measurement = message.Measurement,
            plcCode = message.PlcCode,
            channelCode = message.ChannelCode,
            cycleId = message.CycleId,
            eventType = message.EventType?.ToString(),
            diagnosticType = message.DiagnosticType?.ToString(),
            eventTimestamp = message.Timestamp,
            dataValues = message.DataValues.ToDictionary(
                static kv => kv.Key,
                static kv => kv.Value?.ToString())
        };

        var json = JsonSerializer.Serialize(record) + Environment.NewLine;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(filePath, json).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ─── 内部实现 ──────────────────────────────────

    private async Task<string> WriteInternalAsync(List<DataMessage> dataMessages, bool validateSize)
    {
        var filePath = CreateNewFilePath(dataMessages, _pendingDirectory);
        _writingFiles.TryAdd(filePath, 0);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                await ParquetDataMessageSerializer.WriteAsync(stream, dataMessages).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            if (validateSize)
            {
                var fi = new FileInfo(filePath);
                if (!fi.Exists || fi.Length == 0)
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
            _writingFiles.TryRemove(filePath, out _);
        }
    }

    private async Task<List<DataMessage>> ReadFileInternalAsync(string filePath)
    {
        try
        {
            var fi = new FileInfo(filePath);
            if (!fi.Exists || fi.Length == 0) return [];

            await using var stream = File.OpenRead(filePath);
            return await ParquetDataMessageSerializer.ReadAsync(stream).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parquet 文件读取失败: {FilePath}", filePath);
            throw;
        }
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
        return Path.Combine(directory, $"{measurement}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.parquet");
    }
}
