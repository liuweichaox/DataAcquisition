using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 数据存储服务。
/// </summary>
public interface IDataStorageService
{
    /// <summary>
    /// 保存单条数据消息。
    /// </summary>
    /// <param name="dataMessage">需要保存的数据消息</param>
    /// <returns>表示异步操作的任务。</returns>
    Task SaveAsync(DataMessage dataMessage);

    /// <summary>
    /// 批量保存数据消息。
    /// </summary>
    /// <param name="dataPoints">需要保存的数据消息列表</param>
    /// <returns>表示异步操作的任务。</returns>
    Task SaveBatchAsync(List<DataMessage> dataPoints);

    /// <summary>
    /// 更新记录。
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="values">需要更新的字段及其值</param>
    /// <param name="conditions">更新条件</param>
    /// <returns>表示异步操作的任务。</returns>
    Task UpdateAsync(string tableName, Dictionary<string, object> values, Dictionary<string, object> conditions);
}