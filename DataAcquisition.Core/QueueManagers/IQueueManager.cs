using System;

namespace DataAcquisition.Core.QueueManagers;

/// <summary>
/// 队列管理器接口
/// </summary>
public interface IQueueManager: IDisposable
{
    /// <summary>
    /// 将数据添加到队列
    /// </summary>
    /// <param name="dataMessage">要添加的数据</param>
    void EnqueueData(DataMessage dataMessage);
}