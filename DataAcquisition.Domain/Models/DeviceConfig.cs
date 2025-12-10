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
    public List<DataAcquisitionChannel> Channels { get; set; }
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
/// 条件采集触发配置
/// 用于定义条件采集的开始或结束事件触发规则。
/// 典型应用场景：生产开始记录开始时间，生产结束记录结束时间，记录设备运行状态等。
/// </summary>
public class AcquisitionTrigger
{
    /// <summary>
    /// 触发模式
    /// 定义何时触发采集：Always（无条件）、RisingEdge（上升沿）、FallingEdge（下降沿）等
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TriggerMode TriggerMode { get; set; }

    /// <summary>
    /// 数据操作类型
    /// Insert：插入新记录（通常用于Start事件）
    /// Update：更新现有记录（通常用于End事件，通过cycle_id更新对应的开始记录）
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DataOperation Operation { get; set; } = DataOperation.Insert;

    /// <summary>
    /// 时间戳字段名（Timestamp Field）
    /// 用于记录开始时间或结束时间的字段名
    /// 例如："start_time" 或 "end_time"
    /// </summary>
    public string TimestampField { get; set; }
}

/// <summary>
/// 条件采集配置，包含开始与结束事件
/// 用于实现条件采集：根据PLC寄存器状态判断何时开始采集，何时结束采集。
///
/// 典型应用场景：
/// - 生产周期管理：生产开始（Start）时写入Start事件数据点，生产结束（End）时写入End事件数据点
/// - 设备状态监控：设备启动（Start）时写入Start事件数据点，设备停止（End）时写入End事件数据点
///
/// 工作原理：
/// 1. 系统持续监控指定的PLC寄存器（Register）
/// 2. 当满足Start触发条件时，生成唯一的cycle_id（GUID），插入新记录并保存cycle_id
/// 3. 当满足End触发条件时，使用cycle_id作为条件更新对应的记录
/// 4. 使用cycle_id而非时间戳作为Update条件，避免并发冲突
///
/// 数据库兼容性：
/// - 时序数据库：cycle_id作为标签（tag），类型为字符串
/// </summary>
public class ConditionalAcquisition
{
    /// <summary>
    /// 触发地址
    /// 用于判断触发条件的PLC寄存器地址
    /// 例如："D6200"（三菱PLC）或 "M100"（位寄存器）
    /// </summary>
    public string? Register { get; set; }

    /// <summary>
    /// 数据类型
    /// 触发寄存器的数据类型：ushort、uint、ulong、short、int、long、float、double
    /// </summary>
    public string? DataType { get; set; }

    /// <summary>
    /// 开始事件配置
    /// 定义何时触发开始采集，通常使用RisingEdge（从0变1）或ValueIncrease（值增加）
    /// 触发时会生成cycle_id并插入新记录
    /// </summary>
    public AcquisitionTrigger? Start { get; set; }

    /// <summary>
    /// 结束事件配置
    /// 定义何时触发结束采集，通常使用FallingEdge（从1变0）或ValueDecrease（值减少）
    /// 触发时会使用cycle_id更新对应的开始记录
    /// </summary>
    public AcquisitionTrigger? End { get; set; }
}

/// <summary>
/// 通道
/// </summary>
[JsonConverter(typeof(DataAcquisitionChannelJsonConverter))]
public class DataAcquisitionChannel
{
    /// <summary>
    /// 条件采集配置，null 表示持续采集（无条件采集）
    /// 如果配置了ConditionalAcquisition，则根据触发条件进行条件采集
    /// 如果为null，则按照采集频率持续采集所有数据点
    /// </summary>
    public ConditionalAcquisition? ConditionalAcquisition { get; set; }

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
    /// 测量值名称（Measurement），时序数据库中的表名/测量值标识
    /// </summary>
    public string Measurement { get; set; }

    /// <summary>
    /// 批量保存大小
    /// </summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>
    /// 采集频率间隔（毫秒），用于无条件采集时的采集频率控制
    /// 0 表示最高频率采集（无延迟），大于0表示每次采集后延迟指定毫秒数
    /// 默认值：100（毫秒）
    /// </summary>
    public int AcquisitionInterval { get; set; } = 100;

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
    /// 字段名称（Field Name），时序数据库中存储数值的字段名
    /// </summary>
    public string FieldName { get; set; }

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