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
    ///     PLC编码[Tag]
    /// </summary>
    public required string PlcCode { get; init; }

    /// <summary>
    ///     通道编码[Tag]
    /// </summary>
    public required string ChannelCode { get; init; }

    /// <summary>
    ///     事件类型：start（开始事件）、end（结束事件）、data（普通数据点）[Field]
    /// </summary>
    public EventType EventType { get; private init; }

    /// <summary>
    ///     数据值字典，存储所有采集的数据点值[Field]
    /// </summary>
    public ConcurrentDictionary<string, dynamic?> DataValues { get; } = new();

    /// <summary>
    ///     时间戳[Time]
    /// </summary>
    public DateTime Timestamp { get; private set; }

    public static DataMessage Create(string cycleId, string measurement, string plcCode, string channelCode,
        EventType eventType, DateTime timestamp)
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


    public void AddDataValue(string key, dynamic? value)
    {
        DataValues.TryAdd(key, value);
    }
}

public enum EventType
{
    Start,
    End,
    Data
}