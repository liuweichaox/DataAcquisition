using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Core.DataStorages;

/// <summary>
/// 数据存储服务
/// </summary>
public interface IDataStorage
{
    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="dataPoint"></param>
    Task SaveAsync(DataPoint dataPoint);
    
    /// <summary>
    /// 批量保存
    /// </summary>
    /// <param name="dataPoints"></param>
    Task SaveBatchAsync(List<DataPoint> dataPoints);

    /// <summary>
    /// 释放资源
    /// </summary>
    void Dispose();
}