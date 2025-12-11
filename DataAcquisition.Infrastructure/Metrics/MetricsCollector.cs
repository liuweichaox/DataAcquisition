using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Infrastructure.Metrics;

/// <summary>
/// 基于 System.Diagnostics.Metrics 的指标收集器实现
/// </summary>
public class MetricsCollector : IMetricsCollector
{
    private readonly Meter _meter;
    private readonly Histogram<double> _collectionLatencyHistogram;
    private readonly Histogram<double> _collectionRateHistogram;
    private readonly Histogram<int> _queueDepthHistogram;
    private readonly Histogram<double> _processingLatencyHistogram;
    private readonly Histogram<double> _writeLatencyHistogram;
    private readonly Histogram<double> _batchWriteEfficiencyHistogram;
    private readonly Counter<long> _errorCounter;
    private readonly Counter<long> _connectionStatusCounter;
    private readonly Histogram<double> _connectionDurationHistogram;

    public MetricsCollector()
    {
        _meter = new Meter("DataAcquisition", "1.0.0");

        // 采集延迟指标
        _collectionLatencyHistogram = _meter.CreateHistogram<double>(
            "data_acquisition.collection_latency_ms",
            "ms",
            "采集延迟（从PLC读取到写入数据库的时间，毫秒）");

        // 采集频率指标
        _collectionRateHistogram = _meter.CreateHistogram<double>(
            "data_acquisition.collection_rate",
            "points/s",
            "采集频率（每秒采集的数据点数）");

        // 队列深度指标
        _queueDepthHistogram = _meter.CreateHistogram<int>(
            "data_acquisition.queue_depth",
            "messages",
            "队列深度（当前待处理消息数）");

        // 处理延迟指标
        _processingLatencyHistogram = _meter.CreateHistogram<double>(
            "data_acquisition.processing_latency_ms",
            "ms",
            "处理延迟（队列处理延迟，毫秒）");

        // 写入延迟指标
        _writeLatencyHistogram = _meter.CreateHistogram<double>(
            "data_acquisition.write_latency_ms",
            "ms",
            "写入延迟（数据库写入延迟，毫秒）");

        // 批量写入效率指标
        _batchWriteEfficiencyHistogram = _meter.CreateHistogram<double>(
            "data_acquisition.batch_write_efficiency",
            "points/ms",
            "批量写入效率（批量大小/写入耗时）");

        // 错误计数
        _errorCounter = _meter.CreateCounter<long>(
            "data_acquisition.errors_total",
            "errors",
            "错误总数（按设备/通道统计）");

        // 连接状态计数
        _connectionStatusCounter = _meter.CreateCounter<long>(
            "data_acquisition.connection_status_changes_total",
            "changes",
            "连接状态变化总数");

        // 连接持续时间
        _connectionDurationHistogram = _meter.CreateHistogram<double>(
            "data_acquisition.connection_duration_seconds",
            "seconds",
            "连接持续时间（秒）");
    }

    public void RecordCollectionLatency(string deviceCode, string measurement, double latencyMs, string? channelCode = null)
    {
        var tagList = new List<KeyValuePair<string, object?>>
        {
            new("plc_code", deviceCode),
            new("measurement", measurement)
        };
        if (!string.IsNullOrEmpty(channelCode))
        {
            tagList.Add(new("channel_code", channelCode));
        }
        _collectionLatencyHistogram.Record(latencyMs, tagList.ToArray());
    }

    public void RecordCollectionRate(string deviceCode, string measurement, double pointsPerSecond, string? channelCode = null)
    {
        var tagList = new List<KeyValuePair<string, object?>>
        {
            new("plc_code", deviceCode),
            new("measurement", measurement)
        };
        if (!string.IsNullOrEmpty(channelCode))
        {
            tagList.Add(new("channel_code", channelCode));
        }
        _collectionRateHistogram.Record(pointsPerSecond, tagList.ToArray());
    }

    public void RecordQueueDepth(int depth)
    {
        _queueDepthHistogram.Record(depth);
    }

    public void RecordProcessingLatency(double latencyMs)
    {
        _processingLatencyHistogram.Record(latencyMs);
    }

    public void RecordWriteLatency(string measurement, double latencyMs)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("measurement", measurement)
        };
        _writeLatencyHistogram.Record(latencyMs, tags);
    }

    public void RecordBatchWriteEfficiency(int batchSize, double latencyMs)
    {
        if (latencyMs > 0)
        {
            var efficiency = batchSize / latencyMs; // points per millisecond
            _batchWriteEfficiencyHistogram.Record(efficiency);
        }
    }

    public void RecordError(string deviceCode, string? measurement = null, string? channelCode = null)
    {
        var tagList = new List<KeyValuePair<string, object?>>
        {
            new("plc_code", deviceCode)
        };
        if (!string.IsNullOrEmpty(measurement))
        {
            tagList.Add(new("measurement", measurement));
        }
        if (!string.IsNullOrEmpty(channelCode))
        {
            tagList.Add(new("channel_code", channelCode));
        }
        _errorCounter.Add(1, tagList.ToArray());
    }

    public void RecordConnectionStatus(string deviceCode, bool isConnected)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("plc_code", deviceCode),
            new("status", isConnected ? "connected" : "disconnected")
        };
        _connectionStatusCounter.Add(1, tags);
    }

    public void RecordConnectionDuration(string deviceCode, double durationSeconds)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("plc_code", deviceCode)
        };
        _connectionDurationHistogram.Record(durationSeconds, tags);
    }
}
