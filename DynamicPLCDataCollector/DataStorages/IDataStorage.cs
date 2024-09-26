using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.DataStorages;

/// <summary>
/// 数据存储服务
/// </summary>
public interface IDataStorage
{
    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="data"></param>
    /// <param name="metricTableConfig"></param>
    void Save(Dictionary<string, object> data, MetricTableConfig metricTableConfig);

    /// <summary>
    /// 释放数据流
    /// </summary>
    void ReleaseAll();
}