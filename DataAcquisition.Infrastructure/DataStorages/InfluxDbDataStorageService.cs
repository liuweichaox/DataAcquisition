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
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.DataStorages;

/// <summary>
/// 使用 InfluxDB 实现的时序数据库存储服务。
/// </summary>
public class InfluxDbDataStorageService : IDataStorageService, IDisposable
{
    private readonly InfluxDBClient _client;
    private readonly string _bucket;
    private readonly string _org;
    private readonly ILogger<InfluxDbDataStorageService> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly System.Diagnostics.Stopwatch _writeStopwatch = new();

    /// <summary>
    /// 构造函数，初始化时序数据库客户端。
    /// </summary>
    public InfluxDbDataStorageService(IConfiguration configuration, ILogger<InfluxDbDataStorageService> logger, IMetricsCollector? metricsCollector = null)
    {
        var url = configuration["InfluxDB:Url"] ?? throw new ArgumentNullException("InfluxDB:Url is not configured.");
        var token = configuration["InfluxDB:Token"] ?? throw new ArgumentNullException("InfluxDB:Token is not configured.");
        _bucket = configuration["InfluxDB:Bucket"] ?? throw new ArgumentNullException("InfluxDB:Bucket is not configured.");
        _org = configuration["InfluxDB:Org"] ?? throw new ArgumentNullException("InfluxDB:Org is not configured.");
        _logger = logger;
        _metricsCollector = metricsCollector;

        _client = InfluxDBClientFactory.Create(url, token.ToCharArray());
    }

    /// <summary>
    /// 保存单条数据消息。
    /// </summary>
    public async Task<bool> SaveAsync(DataMessage dataMessage)
    {
        _writeStopwatch.Restart();
        var writeSuccess = false;
        Exception? writeException = null;
        var resetEvent = new System.Threading.ManualResetEventSlim(false);

        try
        {
            var point = ConvertToPoint(dataMessage);
            using var writeApi = _client.GetWriteApi();

            // 设置错误处理回调，捕获写入失败
            writeApi.EventHandler += (sender, args) =>
            {
                writeException = new Exception($"InfluxDB 写入失败: {args}");
                writeSuccess = false;
                resetEvent.Set();
                _logger.LogError(writeException, "[ERROR] InfluxDB 写入错误事件触发: {Message}", writeException.Message);
            };

            writeApi.WritePoint(_bucket, _org, point);
            writeApi.Flush();

            // 等待足够长的时间来检测错误（InfluxDB 异步写入，错误可能延迟）
            // 增加等待时间到 5 秒，以确保捕获到连接或配置错误
            _logger.LogDebug("等待 InfluxDB 写入响应，最多等待 5 秒...");
            var errorOccurred = resetEvent.Wait(TimeSpan.FromSeconds(5));

            if (errorOccurred)
            {
                // 有错误发生，writeSuccess 已经在回调中设置为 false
                _logger.LogWarning("InfluxDB 写入错误事件已触发");
            }
            else
            {
                // 5 秒内没有错误事件，假设写入成功
                // 注意：这可能不准确，因为 InfluxDB 是异步的，错误可能延迟
                writeSuccess = true;
                _logger.LogDebug("InfluxDB 写入在 5 秒内未检测到错误，假设写入成功");
            }

            _writeStopwatch.Stop();

            if (!writeSuccess)
            {
                throw writeException ?? new Exception("InfluxDB 写入失败");
            }

            _metricsCollector?.RecordWriteLatency(dataMessage.Measurement, _writeStopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(dataMessage.PLCCode ?? "unknown", dataMessage.Measurement, dataMessage.ChannelCode);
            _logger.LogError(ex, "[ERROR] 时序数据库插入失败: {Message}", ex.Message);
            return false;
        }
        finally
        {
            resetEvent.Dispose();
        }
    }

    /// <summary>
    /// 批量保存数据消息。
    /// </summary>
    public async Task<bool> SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return true;

        _writeStopwatch.Restart();
        var writeSuccess = false;
        Exception? writeException = null;
        var resetEvent = new System.Threading.ManualResetEventSlim(false);

        try
        {
            var points = dataMessages.Select(ConvertToPoint).ToList();
            using var writeApi = _client.GetWriteApi();

            // 设置错误处理回调，捕获写入失败
            // 注意：所有错误事件（包括 WriteRetriableErrorEvent）都应被视为失败
            writeApi.EventHandler += (sender, args) =>
            {
                // WriteRetriableErrorEvent 也是错误，需要处理
                writeException = new Exception($"InfluxDB 写入失败: {args.GetType().Name} - {args}");
                writeSuccess = false;
                resetEvent.Set();
                _logger.LogError(writeException, "[ERROR] InfluxDB 写入错误事件触发: {EventType} - {Message}",
                    args.GetType().Name, writeException.Message);
            };

            writeApi.WritePoints(_bucket, _org, points);
            writeApi.Flush();

            // 等待足够长的时间来检测错误（InfluxDB 异步写入，错误可能延迟）
            // 增加等待时间到 5 秒，与 SaveAsync 保持一致，以确保捕获到连接或配置错误
            _logger.LogDebug("等待 InfluxDB 批量写入响应，最多等待 5 秒...");
            var errorOccurred = resetEvent.Wait(TimeSpan.FromSeconds(5));

            if (errorOccurred)
            {
                // 有错误发生，writeSuccess 已经在回调中设置为 false
                _logger.LogWarning("InfluxDB 批量写入错误事件已触发");
            }
            else
            {
                // 5 秒内没有错误事件，假设写入成功
                // 注意：这可能不准确，因为 InfluxDB 是异步的，错误可能延迟
                writeSuccess = true;
                _logger.LogDebug("InfluxDB 批量写入在 5 秒内未检测到错误，假设写入成功");
            }

            _writeStopwatch.Stop();

            if (!writeSuccess)
            {
                throw writeException ?? new Exception("InfluxDB 写入失败");
            }

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
        finally
        {
            resetEvent.Dispose();
        }
    }

    /// <summary>
    /// 将DataMessage转换为时序数据库数据点。
    /// </summary>
    private PointData ConvertToPoint(DataMessage dataMessage)
    {
        // InfluxDB 要求时间戳必须是 UTC 时间
        var utcTimestamp = dataMessage.Timestamp.Kind == DateTimeKind.Utc
            ? dataMessage.Timestamp
            : dataMessage.Timestamp.ToUniversalTime();

        var point = PointData.Measurement(dataMessage.Measurement)
            .Timestamp(utcTimestamp, WritePrecision.Ns);

        // 添加标签（tags）
        if (!string.IsNullOrEmpty(dataMessage.PLCCode))
        {
            point = point.Tag("plc_code", dataMessage.PLCCode);
        }

        if (!string.IsNullOrEmpty(dataMessage.ChannelCode))
        {
            point = point.Tag("channel_code", dataMessage.ChannelCode);
        }

        // 添加event_type标签
        var eventType = dataMessage.EventType ?? EventType.Data;
        point = point.Tag("event_type", eventType.ToString());

        // 添加所有数据值作为字段（fields）
        // 注意：cycle_id 作为 field 而不是 tag，因为它是高基数的唯一标识符（GUID）
        if (!string.IsNullOrEmpty(dataMessage.CycleId))
        {
            point = point.Field("cycle_id", dataMessage.CycleId);
        }

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
