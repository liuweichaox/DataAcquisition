using System.Collections.Generic;

namespace DataAcquisition.Domain.Models;

/// <summary>
///     采集表配置
/// </summary>
public class DeviceConfig
{
    /// <summary>
    ///     配置结构版本。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    ///     是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Plc 编码
    /// </summary>
    public string PlcCode { get; set; } = string.Empty;

    /// <summary>
    ///     主机地址，可以是 IP 或主机名。
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    ///     端口
    /// </summary>
    public ushort Port { get; set; }

    /// <summary>
    ///     驱动标识。配置只认完整 Driver 名称，例如：melsec-a1e。
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    ///     协议附加参数。不同驱动可按需读取自身关心的选项。
    /// </summary>
    public Dictionary<string, string> ProtocolOptions { get; set; } = new();

    /// <summary>
    ///     心跳检测地址
    /// </summary>
    public string HeartbeatMonitorRegister { get; set; } = string.Empty;

    /// <summary>
    ///     心跳检测间隔时间（ms）
    /// </summary>
    public int HeartbeatPollingInterval { get; set; }

    /// <summary>
    ///     采集通道
    /// </summary>
    public List<DataAcquisitionChannel> Channels { get; set; } = new();
}
