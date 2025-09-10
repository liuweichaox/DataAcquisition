using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Domain.Clients;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 定义数据采集操作的接口规范。
/// </summary>
public interface IDataAcquisitionService : IDisposable
{
    /// <summary>
    /// 开始采集任务。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    Task StartCollectionTasks();

    /// <summary>
    /// 停止采集任务。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    Task StopCollectionTasks();

    /// <summary>
    /// 获取 PLC 连接状态。
    /// </summary>
    /// <returns>包含 PLC 编号及其连接状态的有序字典。</returns>
    SortedDictionary<string, bool> GetPlcConnectionStatus();

    /// <summary>
    /// 写入 PLC 寄存器。
    /// </summary>
    /// <param name="plcCode">PLC 编号</param>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WritePlcAsync(string plcCode, string address, object value, string dataType, CancellationToken ct = default);
}