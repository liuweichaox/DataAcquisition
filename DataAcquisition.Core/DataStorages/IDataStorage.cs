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
    /// <param name="dataMessage"></param>
    Task SaveAsync(DataMessage dataMessage);
    
    /// <summary>
    /// 批量保存
    /// </summary>
    /// <param name="dataPoints"></param>
    Task SaveBatchAsync(List<DataMessage> dataPoints);

    /// <summary>
    /// 执行SQL
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="param"></param>
    /// <returns></returns>
    Task ExecuteAsync(string sql, object? param = null);
}