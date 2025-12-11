using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Configuration;

namespace DataAcquisition.Infrastructure.DataStorages;

/// <summary>
/// 使用 InfluxDB 实现的时序数据库存储服务。
/// </summary>
public class InfluxDbDataStorageService : IDataStorageService, IDisposable
{
    private readonly InfluxDBClient _client;
    private readonly string _bucket;
    private readonly string _org;
    private readonly IOperationalEventsService _events;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly System.Diagnostics.Stopwatch _writeStopwatch = new();

    /// <summary>
    /// 构造函数，初始化时序数据库客户端。
    /// </summary>
    public InfluxDbDataStorageService(IConfiguration configuration, IOperationalEventsService events, IMetricsCollector? metricsCollector = null)
    {
        var url = configuration["InfluxDB:Url"] ?? throw new ArgumentNullException("InfluxDB:Url is not configured.");
        var token = configuration["InfluxDB:Token"] ?? throw new ArgumentNullException("InfluxDB:Token is not configured.");
        _bucket = configuration["InfluxDB:Bucket"] ?? throw new ArgumentNullException("InfluxDB:Bucket is not configured.");
        _org = configuration["InfluxDB:Org"] ?? throw new ArgumentNullException("InfluxDB:Org is not configured.");
        _events = events;
        _metricsCollector = metricsCollector;

        _client = InfluxDBClientFactory.Create(url, token.ToCharArray());
    }

    /// <summary>
    /// 保存单条数据消息。
    /// </summary>
    public async Task SaveAsync(DataMessage dataMessage)
    {
        _writeStopwatch.Restart();
        try
        {
            var point = ConvertToPoint(dataMessage);
            using var writeApi = _client.GetWriteApi();
            writeApi.WritePoint(_bucket, _org, point);
            await Task.CompletedTask.ConfigureAwait(false);

            _writeStopwatch.Stop();
            _metricsCollector?.RecordWriteLatency(dataMessage.Measurement, _writeStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(dataMessage.DeviceCode ?? "unknown", dataMessage.Measurement);
            await _events.ErrorAsync($"[ERROR] 时序数据库插入失败: {ex.Message}", ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 批量保存数据消息。
    /// </summary>
    public async Task SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return;

        _writeStopwatch.Restart();
        try
        {
            var points = dataMessages.Select(ConvertToPoint).ToList();
            using var writeApi = _client.GetWriteApi();
            writeApi.WritePoints(_bucket, _org, points);
            await Task.CompletedTask.ConfigureAwait(false);

            _writeStopwatch.Stop();
            var batchSize = dataMessages.Count;
            _metricsCollector?.RecordBatchWriteEfficiency(batchSize, _writeStopwatch.ElapsedMilliseconds);

            // 记录每个测量值的写入延迟
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            _metricsCollector?.RecordWriteLatency(measurement, _writeStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            var deviceCode = dataMessages.FirstOrDefault()?.DeviceCode ?? "unknown";
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            _metricsCollector?.RecordError(deviceCode, measurement);
            await _events.ErrorAsync($"[ERROR] 时序数据库批量插入失败: {ex.Message}", ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 更新记录（时序数据库不支持更新，改为写入新数据点）。
    /// </summary>
    public async Task UpdateAsync(string measurement, Dictionary<string, object> values, Dictionary<string, object> conditions)
    {
        // 时序数据库不支持更新操作，将Update转换为Insert操作
        // 使用event_type="end"标签标识这是End事件
        try
        {
            var point = PointData.Measurement(measurement)
                .Timestamp(DateTime.Now, WritePrecision.Ns);

            // 将conditions中的cycle_id作为tag
            if (conditions.TryGetValue("cycle_id", out var cycleId))
            {
                point = point.Tag("cycle_id", cycleId.ToString() ?? string.Empty);
            }

            // 添加event_type="end"标签
            point = point.Tag("event_type", "end");

            // 添加所有values作为fields
            foreach (var kvp in values)
            {
                var fieldValue = ConvertToFieldValue(kvp.Value);
                point = fieldValue switch
                {
                    string s => point.Field(kvp.Key, s),
                    bool b => point.Field(kvp.Key, b),
                    long l => point.Field(kvp.Key, l),
                    double d => point.Field(kvp.Key, d),
                    _ => point.Field(kvp.Key, fieldValue.ToString() ?? string.Empty)
                };
            }

            using var writeApi = _client.GetWriteApi();
            writeApi.WritePoint(_bucket, _org, point);
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync($"[ERROR] 时序数据库更新（转换为插入）失败: {ex.Message}", ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 将DataMessage转换为时序数据库数据点。
    /// </summary>
    private PointData ConvertToPoint(DataMessage dataMessage)
    {
        var point = PointData.Measurement(dataMessage.Measurement)
            .Timestamp(dataMessage.Timestamp, WritePrecision.Ns);

        // 添加标签（tags）
        if (!string.IsNullOrEmpty(dataMessage.DeviceCode))
        {
            point = point.Tag("device_code", dataMessage.DeviceCode);
        }

        if (!string.IsNullOrEmpty(dataMessage.CycleId))
        {
            point = point.Tag("cycle_id", dataMessage.CycleId);
        }

        // 添加event_type标签
        var eventType = dataMessage.EventType ?? "data";
        point = point.Tag("event_type", eventType);

        // 添加所有数据值作为字段（fields）
        foreach (var kvp in dataMessage.DataValues)
        {
            if (kvp.Value != null)
            {
                var fieldValue = ConvertToFieldValue(kvp.Value);
                point = fieldValue switch
                {
                    string s => point.Field(kvp.Key, s),
                    bool b => point.Field(kvp.Key, b),
                    long l => point.Field(kvp.Key, l),
                    double d => point.Field(kvp.Key, d),
                    _ => point.Field(kvp.Key, fieldValue.ToString() ?? string.Empty)
                };
            }
        }

        return point;
    }

    /// <summary>
    /// 将对象值转换为时序数据库字段值。
    /// </summary>
    private object ConvertToFieldValue(object? value)
    {
        if (value == null)
            return string.Empty;

        return value switch
        {
            string str => str,
            bool b => b,
            byte b => (long)b,
            sbyte sb => (long)sb,
            short s => (long)s,
            ushort us => (long)us,
            int i => (long)i,
            uint ui => (long)ui,
            long l => l,
            ulong ul => (long)ul,
            float f => (double)f,
            double d => d,
            decimal dec => (double)dec,
            DateTime dt => dt.ToString("O"), // ISO 8601格式
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        _client?.Dispose();
    }
}
