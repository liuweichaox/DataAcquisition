using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.Queues;

/// <summary>
///     批次持久化协调器。负责执行 WAL-first 链路，并处理主存储失败、毒消息隔离和回退策略。
/// </summary>
internal sealed class QueueBatchPersister
{
    private readonly IMetricsCollector? _metricsCollector;
    private readonly IDataStorageService _primaryStorage;
    private readonly IWalStorageService _walStorage;
    private readonly ILogger _logger;

    public QueueBatchPersister(
        IWalStorageService walStorage,
        IDataStorageService primaryStorage,
        ILogger logger,
        IMetricsCollector? metricsCollector)
    {
        _walStorage = walStorage;
        _primaryStorage = primaryStorage;
        _logger = logger;
        _metricsCollector = metricsCollector;
    }

    public async Task<bool> PersistAsync(string measurement, List<DataMessage> messages)
    {
        if (messages.Count == 0)
            return true;

        string? walPath = null;
        try
        {
            walPath = await _walStorage.WriteAsync(messages).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return await HandleWalWriteFailureAsync(measurement, messages, ex).ConfigureAwait(false);
        }

        return await PersistToPrimaryStorageAsync(measurement, messages, walPath).ConfigureAwait(false);
    }

    private async Task<bool> PersistToPrimaryStorageAsync(string measurement, List<DataMessage> messages, string walPath)
    {
        try
        {
            var success = await _primaryStorage.SaveBatchAsync(messages).ConfigureAwait(false);
            if (success)
            {
                await _walStorage.DeleteAsync(walPath).ConfigureAwait(false);
                return true;
            }

            await MoveWalToRetryAsync(walPath, measurement, messages).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "主存储写入异常 {Measurement}: {Message}", measurement, ex.Message);

            try
            {
                await _walStorage.MoveToRetryAsync(walPath).ConfigureAwait(false);
                _logger.LogWarning("主存储异常，WAL 已移入重试队列: {WalPath}", walPath);
                return true;
            }
            catch (Exception moveEx)
            {
                _logger.LogError(moveEx, "WAL 移入重试队列失败 {WalPath}: {Message}", walPath, moveEx.Message);
                return false;
            }
        }
    }

    private async Task MoveWalToRetryAsync(string walPath, string measurement, List<DataMessage> messages)
    {
        await _walStorage.MoveToRetryAsync(walPath).ConfigureAwait(false);
        var first = messages.FirstOrDefault();
        _metricsCollector?.RecordError(first?.PlcCode ?? "unknown", measurement, first?.ChannelCode);
        _logger.LogWarning("主存储写入失败，WAL 已移入重试队列: {WalPath}", walPath);
    }

    private async Task<bool> HandleWalWriteFailureAsync(string measurement, List<DataMessage> messages, Exception ex)
    {
        if (messages.Count == 0)
            return true;

        if (IsTransientWalException(ex))
        {
            _logger.LogError(ex, "WAL 持久化失败 {Measurement}: {Message}", measurement, ex.Message);
            return false;
        }

        if (messages.Count == 1)
            return await QuarantineInvalidMessageAsync(messages[0], ex).ConfigureAwait(false);

        _logger.LogWarning(ex,
            "批量 WAL 写入失败，开始逐条降级写入: {Measurement}, Count={Count}",
            measurement, messages.Count);

        foreach (var message in messages)
        {
            var handled = await PersistSingleMessageAsync(message).ConfigureAwait(false);
            if (!handled)
                return false;
        }

        return true;
    }

    private async Task<bool> PersistSingleMessageAsync(DataMessage message)
    {
        try
        {
            return await PersistAsync(message.Measurement, [message]).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return await QuarantineInvalidMessageAsync(message, ex).ConfigureAwait(false);
        }
    }

    private async Task<bool> QuarantineInvalidMessageAsync(DataMessage message, Exception ex)
    {
        try
        {
            await _walStorage.QuarantineInvalidAsync(message, ex.Message).ConfigureAwait(false);
            _metricsCollector?.RecordError(message.PlcCode, message.Measurement, message.ChannelCode);
            _logger.LogError(ex,
                "检测到无法写入 WAL 的坏消息，已隔离到 invalid 目录: {PlcCode}-{ChannelCode}-{Measurement}",
                message.PlcCode, message.ChannelCode, message.Measurement);
            return true;
        }
        catch (Exception quarantineEx)
        {
            _logger.LogError(quarantineEx,
                "坏消息隔离失败，批次仍需回补重试: {PlcCode}-{ChannelCode}-{Measurement}",
                message.PlcCode, message.ChannelCode, message.Measurement);
            return false;
        }
    }

    private static bool IsTransientWalException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException;
}
