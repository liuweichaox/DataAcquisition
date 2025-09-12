using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DataAcquisition.Domain.Models;

/// <summary>
/// 采集表配置
/// </summary>
public class DeviceConfig
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// PLC 编码
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// IP 地址
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// 端口
    /// </summary>
    public ushort Port { get; set; }

    /// <summary>
    /// PLC 类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlcType Type { get; set; }

    /// <summary>
    /// 心跳检测地址
    /// </summary>
    public string HeartbeatMonitorRegister { get; set; }

    /// <summary>
    /// 心跳检测间隔时间（ms）
    /// </summary>
    public int HeartbeatPollingInterval { get; set; }

    /// <summary>
    /// 采集通道
    /// </summary>
    public List<Channel> Channels { get; set; }
}

/// <summary>
/// 触发模式
/// </summary>
public enum TriggerMode
{
    /// <summary>
    /// 无条件触发
    /// </summary>
    Always,
    /// <summary>
    /// 数值增加时触发
    /// </summary>
    ValueIncrease,
    /// <summary>
    /// 数值减少时触发
    /// </summary>
    ValueDecrease,
    /// <summary>
    /// 上升沿触发（寄存器从 0 变为 1 时采集）
    /// </summary>
    RisingEdge,
    /// <summary>
    /// 下降沿触发（寄存器从 1 变为 0 时采集）
    /// </summary>
    FallingEdge
}

/// <summary>
/// 生命周期触发配置
/// </summary>
public class LifecycleEvent
{
    /// <summary>
    /// 触发模式
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TriggerMode TriggerMode { get; set; }

    /// <summary>
    /// 数据操作类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DataOperation Operation { get; set; } = DataOperation.Insert;

    /// <summary>
    /// 时间戳列名
    /// </summary>
    public string StampColumn { get; set; }
}

/// <summary>
/// 生命周期配置，包含开始与结束事件
/// </summary>
public class Lifecycle
{
    /// <summary>
    /// 触发地址
    /// </summary>
    public string? Register { get; set; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public string? DataType { get; set; }

    /// <summary>
    /// 开始事件
    /// </summary>
    public LifecycleEvent? Start { get; set; }

    /// <summary>
    /// 结束事件
    /// </summary>
    public LifecycleEvent? End { get; set; }
}

/// <summary>
/// 通道
/// </summary>
public class Channel
{
    /// <summary>
    /// 通道标识
    /// </summary>
    public string ChannelId { get; set; }

    /// <summary>
    /// 通道名称
    /// </summary>
    public string ChannelName { get; set; }

    /// <summary>
    /// 生命周期配置，null 表示持续采集
    /// </summary>
    public Lifecycle? Lifecycle { get; set; }

    /// <summary>
    /// 是否启用批量读取
    /// </summary>
    public bool EnableBatchRead { get; set; } = true;

    /// <summary>
    /// 批量读取地址
    /// </summary>
    public string BatchReadRegister { get; set; }

    /// <summary>
    /// 批量读取长度
    /// </summary>
    public ushort BatchReadLength { get; set; }
    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 批量保存大小
    /// </summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>
    /// 采集位置配置
    /// </summary>
    public List<DataPoint>? DataPoints { get; set; }
}

/// <summary>
/// 采集位置配置
/// </summary>
public class DataPoint
{
    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// 寄存器地址
    /// </summary>
    public string Register { get; set; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; }

    /// <summary>
    /// 索引位置
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 字符串的字节长度
    /// </summary>
    public int StringByteLength { get; set; }

    /// <summary>
    /// 字符串编码，仅在数据类型为字符串时使用
    /// </summary>
    public string Encoding { get; set; }

    /// <summary>
    /// 数值转换表达式
    /// </summary>
    public string EvalExpression { get; set; }
}