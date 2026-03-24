using System;
using System.Collections.Concurrent;

namespace DataAcquisition.Domain.Models;

/// <summary>
///     数据点消息
/// </summary>
public class DataMessage
{
    /// <summary>
    ///     采集周期唯一标识符（GUID），用于条件采集的Start/End事件关联[Field]
    /// </summary>
    public required string CycleId { get; init; }

    /// <summary>
    ///     测量[Measurement]
    /// </summary>
    public required string Measurement { get; init; }

    /// <summary>
    ///     Plc编码[Tag]
    /// </summary>
    public required string PlcCode { get; init; }

    /// <summary>
    ///     通道编码[Tag]
    /// </summary>
    public required string ChannelCode { get; init; }

    /// <summary>
    ///     正式业务事件类型：start、end、data。[Field]
    /// </summary>
    public EventType? EventType { get; private init; }

    /// <summary>
    ///     诊断事件类型，仅用于恢复/异常审计，不参与正式周期口径。[Field]
    /// </summary>
    public DiagnosticEventType? DiagnosticType { get; private init; }

    /// <summary>
    ///     数据值字典，存储所有采集的数据点值[Field]
    /// </summary>
    public ConcurrentDictionary<string, object?> DataValues { get; } = new();

    /// <summary>
    ///     时间戳[Time]
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    public static DataMessage Create(string cycleId, string measurement, string plcCode, string channelCode,
        EventType eventType, DateTimeOffset timestamp)
    {
        return new DataMessage
        {
            CycleId = cycleId,
            Measurement = measurement,
            PlcCode = plcCode,
            ChannelCode = channelCode,
            EventType = eventType,
            Timestamp = timestamp
        };
    }

    public static DataMessage CreateDiagnostic(
        string cycleId,
        string measurement,
        string plcCode,
        string channelCode,
        DiagnosticEventType diagnosticType,
        DateTimeOffset timestamp)
    {
        return new DataMessage
        {
            CycleId = cycleId,
            Measurement = measurement,
            PlcCode = plcCode,
            ChannelCode = channelCode,
            DiagnosticType = diagnosticType,
            Timestamp = timestamp
        };
    }

    public bool AddDataValue(string key, object? value)
    {
        return DataValues.TryAdd(key, DataValueNormalizer.Normalize(value));
    }

    public bool UpdateDataValue(string key, object? newValue, object? originalValue)
    {
        return DataValues.TryUpdate(
            key,
            DataValueNormalizer.Normalize(newValue),
            DataValueNormalizer.Normalize(originalValue));
    }
}

public enum EventType
{
    Start,
    End,
    Data
}

public enum DiagnosticEventType
{
    RecoveredStart,
    Interrupted
}
