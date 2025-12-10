using System;
using System.Collections.Concurrent;

namespace DataAcquisition.Domain.Models;

/// <summary>
/// 数据点消息
/// </summary>
public class DataMessage(DateTime timestamp, string measurement, int batchSize, DataOperation operation = DataOperation.Insert)
{
    public DateTime Timestamp => timestamp;
    public string Measurement => measurement;
    public int BatchSize => batchSize;
    public DataOperation Operation => operation;

    /// <summary>
    /// 采集周期唯一标识符（GUID），用于条件采集的Start/End事件关联
    /// </summary>
    public string? CycleId { get; set; }

    /// <summary>
    /// 设备编码，用于时序数据库标签（tags）
    /// </summary>
    public string? DeviceCode { get; set; }

    /// <summary>
    /// 事件类型：start（开始事件）、end（结束事件）、data（普通数据点）
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// 数据值字典，存储所有采集的数据点值
    /// </summary>
    public ConcurrentDictionary<string, dynamic?> DataValues { get; } = new();
}
