using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Services.DataStorages;

/// <summary>
/// 数据存储服务
/// </summary>
public interface IDataStorage : IAsyncDisposable
{
    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="dataPoint"></param>
    Task SaveAsync(DataPoint? dataPoint);
    
    /// <summary>
    /// 批量保存
    /// </summary>
    /// <param name="dataPoints"></param>
    Task SaveBatchAsync(List<DataPoint?> dataPoints);
}