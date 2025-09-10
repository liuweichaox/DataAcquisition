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
}