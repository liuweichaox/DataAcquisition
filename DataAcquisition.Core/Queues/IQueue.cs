using System;

namespace DataAcquisition.Core.Queues;

/// <summary>
/// 队列接口
/// </summary>
public interface IQueue : IDisposable
{
    /// <summary>
    /// 将数据添加到队列
    /// </summary>
    /// <param name="dataMessage">要添加的数据</param>
    void EnqueueData(DataMessage dataMessage);
}