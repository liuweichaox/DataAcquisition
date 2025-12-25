namespace DataAcquisition.Domain.Models;

/// <summary>
///     数据采集系统配置选项
/// </summary>
public class AcquisitionOptions
{
    /// <summary>
    ///     通道采集器配置
    /// </summary>
    public ChannelCollectorOptions ChannelCollector { get; set; } = new();

    /// <summary>
    ///     队列服务配置
    /// </summary>
    public QueueServiceOptions QueueService { get; set; } = new();

    /// <summary>
    ///     设备配置服务配置
    /// </summary>
    public DeviceConfigServiceOptions DeviceConfigService { get; set; } = new();
}

/// <summary>
///     通道采集器配置选项
/// </summary>
public class ChannelCollectorOptions
{
    /// <summary>
    ///     连接检查失败时的重试延迟（毫秒）
    ///     默认：100ms
    /// </summary>
    public int ConnectionCheckRetryDelayMs { get; init; } = 100;

    /// <summary>
    ///     触发条件未满足时的等待延迟（毫秒）
    ///     默认：100ms
    /// </summary>
    public int TriggerWaitDelayMs { get; init; } = 100;
}

/// <summary>
///     队列服务配置选项
/// </summary>
public class QueueServiceOptions
{
    /// <summary>
    ///     定时刷新批次间隔（秒）
    ///     默认：5秒
    /// </summary>
    public int FlushIntervalSeconds { get; init; } = 5;

    /// <summary>
    ///     失败批次重试间隔（秒）
    ///     默认：10秒
    /// </summary>
    public int RetryIntervalSeconds { get; init; } = 10;

    /// <summary>
    ///     最大重试次数
    ///     默认：3次
    /// </summary>
    public int MaxRetryCount { get; init; } = 3;
}

/// <summary>
///     设备配置服务配置选项
/// </summary>
public class DeviceConfigServiceOptions
{
    /// <summary>
    ///     配置变更检测延迟（毫秒）
    ///     默认：500ms
    /// </summary>
    public int ConfigChangeDetectionDelayMs { get; set; } = 500;
}