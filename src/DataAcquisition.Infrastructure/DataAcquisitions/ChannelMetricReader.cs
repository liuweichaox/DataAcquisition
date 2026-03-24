using System;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
///     通道指标读取器。负责按配置从 PLC 读取指标值并填充到消息中。
/// </summary>
internal static class ChannelMetricReader
{
    public static async Task ReadAsync(
        IPlcDataAccessClient client,
        DataAcquisitionChannel channel,
        DataMessage dataMessage,
        ILogger logger)
    {
        if (channel.Metrics == null)
            return;

        if (channel.EnableBatchRead)
        {
            await ReadBatchAsync(client, channel, dataMessage, logger).ConfigureAwait(false);
            return;
        }

        await ReadIndividuallyAsync(client, channel, dataMessage, logger).ConfigureAwait(false);
    }

    private static async Task ReadBatchAsync(
        IPlcDataAccessClient client,
        DataAcquisitionChannel channel,
        DataMessage dataMessage,
        ILogger logger)
    {
        var batchData = await client.ReadAsync(channel.BatchReadRegister, channel.BatchReadLength)
            .ConfigureAwait(false);
        var buffer = batchData.Content;

        foreach (var metric in channel.Metrics!)
        {
            try
            {
                var value = PlcValueAccessor.Decode(client, buffer, metric.Index, metric.StringByteLength,
                    metric.DataType, metric.Encoding);
                dataMessage.AddDataValue(metric.FieldName, value);
            }
            catch (Exception ex)
            {
                LogMetricReadFailure(logger, dataMessage, metric.FieldName, ex);
            }
        }
    }

    private static async Task ReadIndividuallyAsync(
        IPlcDataAccessClient client,
        DataAcquisitionChannel channel,
        DataMessage dataMessage,
        ILogger logger)
    {
        foreach (var metric in channel.Metrics!)
        {
            try
            {
                var value = await PlcValueAccessor.ReadAsync(client, metric.Register, metric.DataType,
                        metric.StringByteLength, metric.Encoding)
                    .ConfigureAwait(false);
                dataMessage.AddDataValue(metric.FieldName, value);
            }
            catch (Exception ex)
            {
                LogMetricReadFailure(logger, dataMessage, metric.FieldName, ex);
            }
        }
    }

    private static void LogMetricReadFailure(ILogger logger, DataMessage dataMessage, string fieldName, Exception ex)
    {
        logger.LogWarning(ex,
            "{PlcCode}-{ChannelCode}-{Measurement}:指标读取失败，已跳过字段 {FieldName}",
            dataMessage.PlcCode, dataMessage.ChannelCode, dataMessage.Measurement, fieldName);
    }
}
