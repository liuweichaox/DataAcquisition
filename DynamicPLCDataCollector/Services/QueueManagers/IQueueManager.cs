using System.Collections.Generic;

namespace DynamicPLCDataCollector.Services.QueueManagers;

/// <summary>
/// 队列管理器接口
/// </summary>
public interface IQueueManager
{
    /// <summary>
    /// 将数据添加到队列
    /// </summary>
    /// <param name="data">要添加的数据</param>
    void EnqueueData(Dictionary<string, object> data);

    /// <summary>
    /// 完成队列，防止再添加数据
    /// </summary>
    void Complete();
}