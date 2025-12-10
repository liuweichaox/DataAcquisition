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
    /// 更新记录（时序数据库不支持更新，此方法将Update转换为Insert操作）。
    /// 对于End事件，会写入新的数据点，使用event_type="end"标签标识。
    /// </summary>
    /// <param name="measurement">测量值名称（Measurement）</param>
    /// <param name="values">需要写入的字段及其值</param>
    /// <param name="conditions">条件（如cycle_id，将作为标签）</param>
    /// <returns>表示异步操作的任务。</returns>
    Task UpdateAsync(string measurement, Dictionary<string, object> values, Dictionary<string, object> conditions);
}