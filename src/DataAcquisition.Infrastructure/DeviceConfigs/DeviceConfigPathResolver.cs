using System;
using System.IO;

namespace DataAcquisition.Infrastructure.DeviceConfigs;

/// <summary>
///     统一解析设备配置目录，确保服务运行和离线校验使用相同的路径规则。
/// </summary>
public static class DeviceConfigPathResolver
{
    public static string Resolve(string? configDirectory)
    {
        var directory = string.IsNullOrWhiteSpace(configDirectory) ? "Configs" : configDirectory.Trim();
        return Path.IsPathRooted(directory)
            ? directory
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, directory));
    }
}
