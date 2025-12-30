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
///     使用 InfluxDB 实现的时序数据库存储服务。
/// </summary>
public class InfluxDbDataStorageService : IDataStorageService, IDisposable
{
    private readonly string _bucket;
    private readonly InfluxDBClient _client;
    private readonly ILogger<InfluxDbDataStorageService> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly string _org;
    private readonly Stopwatch _writeStopwatch = new();

    /// <summary>
    ///     构造函数，初始化时序数据库客户端。
    /// </summary>
    public InfluxDbDataStorageService(IConfiguration configuration, ILogger<InfluxDbDataStorageService> logger,
        IMetricsCollector? metricsCollector = null)
    {
        var url = configuration["InfluxDB:Url"] ?? throw new ArgumentNullException("InfluxDB:Url is not configured.");
        var token = configuration["InfluxDB:Token"] ??
                    throw new ArgumentNullException("InfluxDB:Token is not configured.");
        _bucket = configuration["InfluxDB:Bucket"] ??
                  throw new ArgumentNullException("InfluxDB:Bucket is not configured.");
        _org = configuration["InfluxDB:Org"] ?? throw new ArgumentNullException("InfluxDB:Org is not configured.");
        _logger = logger;
        _metricsCollector = metricsCollector;

        // InfluxDB.Client 4.x 推荐直接使用客户端初始化方式
        _client = new InfluxDBClient(url, token);
    }

    /// <summary>
    ///     批量保存数据消息。
    /// </summary>
    public async Task<bool> SaveBatchAsync(List<DataMessage>? dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return true;

        _writeStopwatch.Restart();

        try
        {
            var points = dataMessages.Select(ConvertToPoint).ToList();
            var writeApi = _client.GetWriteApiAsync();
            await writeApi.WritePointsAsync(points, _bucket, _org);
            _writeStopwatch.Stop();

            var batchSize = dataMessages.Count;
            _metricsCollector?.RecordBatchWriteEfficiency(batchSize, _writeStopwatch.ElapsedMilliseconds);

            // 记录每个测量值的写入延迟
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            _metricsCollector?.RecordWriteLatency(measurement, _writeStopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            var plcCode = dataMessages.FirstOrDefault()?.PLCCode ?? "unknown";
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            var channelCode = dataMessages.FirstOrDefault()?.ChannelCode;
            _metricsCollector?.RecordError(plcCode, measurement, channelCode);
            _logger.LogError(ex, "[ERROR] 时序数据库批量插入失败: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     释放资源。
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
    }

    /// <summary>
    ///     将DataMessage转换为时序数据库数据点。
    /// </summary>
    private PointData ConvertToPoint(DataMessage dataMessage)
    {
        // 注意：InfluxDB 要求时间戳必须是 UTC 时间，所以需要转换
        // 系统内部使用本地时间，但写入 InfluxDB 时需要转换为 UTC
        var utcTimestamp = dataMessage.Timestamp.Kind == DateTimeKind.Utc
            ? dataMessage.Timestamp
            : dataMessage.Timestamp.ToUniversalTime();

        var point = PointData.Measurement(dataMessage.Measurement)
            .Timestamp(utcTimestamp, WritePrecision.Ns);

        // 添加标签（tags）
        if (!string.IsNullOrEmpty(dataMessage.PLCCode)) point = point.Tag("plc_code", dataMessage.PLCCode);

        if (!string.IsNullOrEmpty(dataMessage.ChannelCode)) point = point.Tag("channel_code", dataMessage.ChannelCode);

        // 添加event_type标签
        var eventType = dataMessage.EventType;
        point = point.Tag("event_type", eventType.ToString());

        // 添加所有数据值作为字段（fields）
        // 注意：cycle_id 作为 field 而不是 tag，因为它是高基数的唯一标识符（GUID）
        if (!string.IsNullOrEmpty(dataMessage.CycleId)) point = point.Field("cycle_id", dataMessage.CycleId);

        foreach (var kvp in dataMessage.DataValues)
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

    /// <summary>
    ///     将对象值转换为时序数据库字段值。
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
}