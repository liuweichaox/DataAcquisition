using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 采集配置服务接口。
/// </summary>
public interface IDeviceConfigService
{
    /// <summary>
    /// 获取所有采集表格配置。
    /// </summary>
    /// <returns>包含所有采集表格配置的列表。</returns>
    Task<List<DeviceConfig>> GetConfigs();

    /// <summary>
    /// 配置变更事件，当配置文件发生变化时触发
    /// </summary>
    event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    /// <param name="config">要验证的配置</param>
    /// <returns>验证结果，包含是否有效和错误信息</returns>
    Task<ConfigValidationResult> ValidateConfigAsync(DeviceConfig config);
}

/// <summary>
/// 配置变更事件参数
/// </summary>
public class ConfigChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变更类型
    /// </summary>
    public ConfigChangeType ChangeType { get; set; }

    /// <summary>
    /// PLC编码（PLCCode），用于标识设备
    /// </summary>
    public string PLCCode { get; set; } = string.Empty;

    /// <summary>
    /// 新的配置（如果是删除则为null）
    /// </summary>
    public DeviceConfig? NewConfig { get; set; }

    /// <summary>
    /// 旧的配置（如果是新增则为null）
    /// </summary>
    public DeviceConfig? OldConfig { get; set; }
}

/// <summary>
/// 配置变更类型
/// </summary>
public enum ConfigChangeType
{
    /// <summary>
    /// 新增配置
    /// </summary>
    Added,

    /// <summary>
    /// 更新配置
    /// </summary>
    Updated,

    /// <summary>
    /// 删除配置
    /// </summary>
    Removed
}

/// <summary>
/// 配置验证结果
/// </summary>
public class ConfigValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误信息列表
    /// </summary>
    public List<string> Errors { get; set; } = new();
}