using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Services.DataAcquisitionConfigs;

/// <summary>
/// 采集配置服务接口
/// </summary>
public interface IDataAcquisitionConfigService
{
    /// <summary>
    /// 获取所有采集表格配置
    /// </summary>
    /// <returns></returns>
    Task<List<DataAcquisitionConfig>> GetConfigs();
}