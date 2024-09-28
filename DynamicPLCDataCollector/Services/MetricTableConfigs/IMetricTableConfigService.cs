using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.Services.MetricTableConfigs;

/// <summary>
/// 采集配置服务接口
/// </summary>
public interface IMetricTableConfigService
{
    /// <summary>
    /// 获取所有采集表格配置
    /// </summary>
    /// <returns></returns>
    Task<List<MetricTableConfig>> GetMetricTableConfigs();
}