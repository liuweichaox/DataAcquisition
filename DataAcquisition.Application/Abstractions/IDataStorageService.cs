using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 数据存储服务
/// </summary>
public interface IDataStorageService
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
    /// 更新记录
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="values"></param>
    /// <param name="conditions"></param>
    Task UpdateAsync(string tableName, Dictionary<string, object> values, Dictionary<string, object> conditions);
}