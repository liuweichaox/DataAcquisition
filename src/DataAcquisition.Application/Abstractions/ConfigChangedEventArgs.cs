using System;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
///     配置变更事件参数。
/// </summary>
public class ConfigChangedEventArgs : EventArgs
{
    /// <summary>
    ///     变更类型。
    /// </summary>
    public ConfigChangeType ChangeType { get; set; }

    /// <summary>
    ///     Plc编码（PlcCode），用于标识设备。
    /// </summary>
    public string PlcCode { get; set; } = string.Empty;

    /// <summary>
    ///     新的配置（如果是删除则为null）。
    /// </summary>
    public DeviceConfig? NewConfig { get; set; }

    /// <summary>
    ///     旧的配置（如果是新增则为null）。
    /// </summary>
    public DeviceConfig? OldConfig { get; set; }
}
