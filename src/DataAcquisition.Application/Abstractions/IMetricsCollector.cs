namespace DataAcquisition.Application.Abstractions;

/// <summary>
///     指标收集器接口，用于收集系统性能指标
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    ///     记录采集延迟（从PLC读取到写入数据库的时间，毫秒）
    /// </summary>
    /// <param name="plcCode">PLC编码</param>
    /// <param name="measurement">测量值名称</param>
    /// <param name="latencyMs">延迟（毫秒）</param>
    /// <param name="channelCode">通道编码（可选）</param>
    void RecordCollectionLatency(string plcCode, string measurement, double latencyMs, string? channelCode = null);

    /// <summary>
    ///     记录采集频率（每秒采集的数据点数）
    /// </summary>
    /// <param name="plcCode">PLC编码</param>
    /// <param name="measurement">测量值名称</param>
    /// <param name="pointsPerSecond">每秒采集的数据点数</param>
    /// <param name="channelCode">通道编码（可选）</param>
    void RecordCollectionRate(string plcCode, string measurement, double pointsPerSecond, string? channelCode = null);

    /// <summary>
    ///     记录队列深度（Channel待读取 + 批量积累的待处理消息总数）
    /// </summary>
    void RecordQueueDepth(int depth);

    /// <summary>
    ///     记录处理延迟（队列处理延迟，毫秒）
    /// </summary>
    void RecordProcessingLatency(double latencyMs);

    /// <summary>
    ///     记录写入延迟（数据库写入延迟，毫秒）
    /// </summary>
    void RecordWriteLatency(string measurement, double latencyMs);

    /// <summary>
    ///     记录批量写入效率（批量大小、写入耗时）
    /// </summary>
    void RecordBatchWriteEfficiency(int batchSize, double latencyMs);

    /// <summary>
    ///     记录错误（按设备/通道统计）
    /// </summary>
    /// <param name="plcCode">PLC编码</param>
    /// <param name="measurement">测量值名称（可选）</param>
    /// <param name="channelCode">通道编码（可选）</param>
    void RecordError(string plcCode, string? measurement = null, string? channelCode = null);

    /// <summary>
    ///     记录PLC连接状态变化
    /// </summary>
    /// <param name="plcCode">PLC编码</param>
    /// <param name="isConnected"></param>
    void RecordConnectionStatus(string plcCode, bool isConnected);

    /// <summary>
    ///     记录连接持续时间（秒）
    /// </summary>
    /// <param name="plcCode">PLC编码</param>
    /// <param name="durationSeconds"></param>
    void RecordConnectionDuration(string plcCode, double durationSeconds);
}