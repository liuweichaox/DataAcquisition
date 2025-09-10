using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 消息队列服务接口。
/// </summary>
public interface IQueueService : IAsyncDisposable
{
    /// <summary>
    /// 发布数据消息。
    /// </summary>
    /// <param name="dataMessage">待发布的数据消息</param>
    /// <returns>表示异步操作的任务。</returns>
    Task PublishAsync(DataMessage dataMessage);

    /// <summary>
    /// 订阅并处理数据消息。
    /// </summary>
    /// <param name="ct">取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task SubscribeAsync(CancellationToken ct);
}