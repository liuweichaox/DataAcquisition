using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.DataStorages;

/// <summary>
///     基于 InfluxDB 的时序数据存储实现。可通过替换 IDataStorageService 注册切换到其他 TSDB。
/// </summary>
public class InfluxDbDataStorageService : IDataStorageService, IDisposable
{
    private readonly string _bucket;
    private readonly InfluxDBClient _client;
    private readonly ILogger<InfluxDbDataStorageService> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly string _org;

    public InfluxDbDataStorageService(IConfiguration configuration, ILogger<InfluxDbDataStorageService> logger,
        IMetricsCollector? metricsCollector = null)
    {
        var url = configuration["InfluxDB:Url"]
            ?? throw new ArgumentNullException(nameof(configuration), "InfluxDB:Url is not configured.");
        var token = configuration["InfluxDB:Token"]
            ?? throw new ArgumentNullException(nameof(configuration), "InfluxDB:Token is not configured.");
        _bucket = configuration["InfluxDB:Bucket"]
            ?? throw new ArgumentNullException(nameof(configuration), "InfluxDB:Bucket is not configured.");
        _org = configuration["InfluxDB:Org"]
            ?? throw new ArgumentNullException(nameof(configuration), "InfluxDB:Org is not configured.");
        _logger = logger;
        _metricsCollector = metricsCollector;
        _client = new InfluxDBClient(url, token);
    }

    public async Task<bool> SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages.Count == 0) return true;

        var sw = Stopwatch.StartNew();
        try
        {
            var points = dataMessages.Select(ConvertToPoint).ToList();
            await _client.GetWriteApiAsync().WritePointsAsync(points, _bucket, _org);
            sw.Stop();

            _metricsCollector?.RecordBatchWriteEfficiency(dataMessages.Count, sw.ElapsedMilliseconds);
            _metricsCollector?.RecordWriteLatency(
                dataMessages[0].Measurement ?? "unknown", sw.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            var first = dataMessages[0];
            _metricsCollector?.RecordError(first.PlcCode ?? "unknown", first.Measurement, first.ChannelCode);
            _logger.LogError(ex, "时序数据库批量插入失败");
            return false;
        }
    }

    public void Dispose() => _client.Dispose();

    private static PointData ConvertToPoint(DataMessage msg)
    {
        var utcTimestamp = msg.Timestamp.Kind == DateTimeKind.Utc
            ? msg.Timestamp
            : msg.Timestamp.ToUniversalTime();

        var point = PointData.Measurement(msg.Measurement)
            .Timestamp(utcTimestamp, WritePrecision.Ns);

        if (!string.IsNullOrEmpty(msg.PlcCode)) point = point.Tag("plc_code", msg.PlcCode);
        if (!string.IsNullOrEmpty(msg.ChannelCode)) point = point.Tag("channel_code", msg.ChannelCode);
        point = point.Tag("event_type", msg.EventType.ToString());

        if (!string.IsNullOrEmpty(msg.CycleId)) point = point.Field("cycle_id", msg.CycleId);

        foreach (var kvp in msg.DataValues)
        {
            var value = kvp.Value;
            if (value is not null)
            {
                var fieldValue = ConvertToFieldValue(value);
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

    private static object ConvertToFieldValue(object? value)
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
}