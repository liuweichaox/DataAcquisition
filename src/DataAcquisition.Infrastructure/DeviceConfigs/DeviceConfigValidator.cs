using System;
using System.Collections.Generic;
using System.Linq;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.DeviceConfigs;

internal static class DeviceConfigValidator
{
    public static ConfigValidationResult Validate(DeviceConfig config)
    {
        var errors = new List<string>();

        if (config.SchemaVersion != 1)
            errors.Add($"当前仅支持 SchemaVersion=1，实际为 {config.SchemaVersion}");

        if (string.IsNullOrWhiteSpace(config.PlcCode))
            errors.Add("设备编码不能为空");

        if (string.IsNullOrWhiteSpace(config.Driver))
            errors.Add("Driver 不能为空");

        if (string.IsNullOrWhiteSpace(config.Host))
            errors.Add("主机地址不能为空");
        else if (!IsValidHost(config.Host))
            errors.Add($"无效的主机地址: {config.Host}");

        if (config.Port == 0)
            errors.Add("端口不能为0");

        if (string.IsNullOrWhiteSpace(config.HeartbeatMonitorRegister))
            errors.Add("心跳检测地址不能为空");

        if (config.HeartbeatPollingInterval <= 0)
            errors.Add("心跳检测间隔必须大于 0");

        if (config.Channels is not { Count: > 0 })
        {
            errors.Add("至少需要配置一个采集通道");
        }
        else
        {
            var duplicateChannels = config.Channels
                .Where(static channel =>
                    !string.IsNullOrWhiteSpace(channel.ChannelCode) && !string.IsNullOrWhiteSpace(channel.Measurement))
                .GroupBy(static channel => $"{channel.ChannelCode}|{channel.Measurement}", StringComparer.OrdinalIgnoreCase)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .ToArray();

            foreach (var duplicateChannel in duplicateChannels)
            {
                var parts = duplicateChannel.Split('|', 2);
                errors.Add($"存在重复的通道定义: ChannelCode={parts[0]}, Measurement={parts[1]}");
            }

            for (var i = 0; i < config.Channels.Count; i++)
                ValidateChannel(config.Channels[i], i + 1, errors);
        }

        return new ConfigValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static bool IsValidHost(string host)
    {
        var hostNameType = Uri.CheckHostName(host.Trim());
        return hostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;
    }

    private static void ValidateChannel(DataAcquisitionChannel channel, int index, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(channel.ChannelCode))
            errors.Add($"通道 {index} 的 ChannelCode 不能为空");

        if (string.IsNullOrWhiteSpace(channel.Measurement))
            errors.Add($"通道 {index} 的 Measurement 不能为空");

        if (channel.BatchSize <= 0)
            errors.Add($"通道 {index} 的 BatchSize 必须大于 0");

        if (channel.AcquisitionInterval < 0)
            errors.Add($"通道 {index} 的 AcquisitionInterval 不能小于 0");

        if (channel.EnableBatchRead)
        {
            if (string.IsNullOrWhiteSpace(channel.BatchReadRegister))
                errors.Add($"通道 {index} 启用批量读取时必须配置 BatchReadRegister");

            if (channel.BatchReadLength == 0)
                errors.Add($"通道 {index} 启用批量读取时 BatchReadLength 必须大于 0");
        }

        if (channel.AcquisitionMode == AcquisitionMode.Conditional)
            ValidateConditionalAcquisition(channel, index, errors);

        if (channel.AcquisitionMode == AcquisitionMode.Always && channel.Metrics is not { Count: > 0 })
            errors.Add($"通道 {index} 在 Always 模式下至少需要一个 Metric");

        if (channel.Metrics is { Count: > 0 })
            ValidateMetrics(channel, index, errors);
    }

    private static void ValidateConditionalAcquisition(DataAcquisitionChannel channel, int index, List<string> errors)
    {
        if (channel.ConditionalAcquisition == null)
        {
            errors.Add($"通道 {index} 在 Conditional 模式下必须配置 ConditionalAcquisition");
            return;
        }

        if (string.IsNullOrWhiteSpace(channel.ConditionalAcquisition.Register))
            errors.Add($"通道 {index} 的 ConditionalAcquisition.Register 不能为空");

        if (string.IsNullOrWhiteSpace(channel.ConditionalAcquisition.DataType))
            errors.Add($"通道 {index} 的 ConditionalAcquisition.DataType 不能为空");

        if (channel.ConditionalAcquisition.StartTriggerMode == null)
            errors.Add($"通道 {index} 的 ConditionalAcquisition.StartTriggerMode 不能为空");

        if (channel.ConditionalAcquisition.EndTriggerMode == null)
            errors.Add($"通道 {index} 的 ConditionalAcquisition.EndTriggerMode 不能为空");
    }

    private static void ValidateMetrics(DataAcquisitionChannel channel, int index, List<string> errors)
    {
        var duplicateFields = channel.Metrics!
            .Where(static metric => !string.IsNullOrWhiteSpace(metric.FieldName))
            .GroupBy(static metric => metric.FieldName, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        foreach (var duplicateField in duplicateFields)
            errors.Add($"通道 {index} 存在重复的 FieldName: {duplicateField}");

        for (var metricIndex = 0; metricIndex < channel.Metrics!.Count; metricIndex++)
        {
            var metric = channel.Metrics[metricIndex];
            var metricPrefix = $"通道 {index} 的 Metric {metricIndex + 1}";

            if (string.IsNullOrWhiteSpace(metric.FieldName))
                errors.Add($"{metricPrefix} 的 FieldName 不能为空");

            if (string.IsNullOrWhiteSpace(metric.Register))
                errors.Add($"{metricPrefix} 的 Register 不能为空");

            if (string.IsNullOrWhiteSpace(metric.DataType))
                errors.Add($"{metricPrefix} 的 DataType 不能为空");

            if (string.Equals(metric.DataType, "string", StringComparison.OrdinalIgnoreCase) &&
                metric.StringByteLength <= 0)
                errors.Add($"{metricPrefix} 的 StringByteLength 必须大于 0");
        }
    }
}
