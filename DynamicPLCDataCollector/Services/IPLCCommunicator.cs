using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.Services;

/// <summary>
/// PLC通讯类
/// </summary>
public interface IPLCCommunicator
{
    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="device"></param>
    /// <param name="metricTableConfig"></param>
    /// <returns></returns>
    Task<Dictionary<string, object>> ReadAsync(Device device, MetricTableConfig metricTableConfig);
    
    /// <summary>
    /// 释放连接
    /// </summary>
    /// <returns></returns>
    Task DisconnectAllAsync();
}