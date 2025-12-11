using System;
using System.Diagnostics.Metrics;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 指标收集器接口，用于收集系统性能指标
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// 记录采集延迟（从PLC读取到写入数据库的时间，毫秒）
    /// </summary>
    void RecordCollectionLatency(string deviceCode, string measurement, double latencyMs);

    /// <summary>
    /// 记录采集频率（每秒采集的数据点数）
    /// </summary>
    void RecordCollectionRate(string deviceCode, string measurement, double pointsPerSecond);

    /// <summary>
    /// 记录队列深度（当前待处理消息数）
    /// </summary>
    void RecordQueueDepth(int depth);

    /// <summary>
    /// 记录处理延迟（队列处理延迟，毫秒）
    /// </summary>
    void RecordProcessingLatency(double latencyMs);

    /// <summary>
    /// 记录写入延迟（数据库写入延迟，毫秒）
    /// </summary>
    void RecordWriteLatency(string measurement, double latencyMs);

    /// <summary>
    /// 记录批量写入效率（批量大小、写入耗时）
    /// </summary>
    void RecordBatchWriteEfficiency(int batchSize, double latencyMs);

    /// <summary>
    /// 记录错误（按设备/通道统计）
    /// </summary>
    void RecordError(string deviceCode, string? measurement = null);

    /// <summary>
    /// 记录PLC连接状态变化
    /// </summary>
    void RecordConnectionStatus(string deviceCode, bool isConnected);

    /// <summary>
    /// 记录连接持续时间（秒）
    /// </summary>
    void RecordConnectionDuration(string deviceCode, double durationSeconds);
}
