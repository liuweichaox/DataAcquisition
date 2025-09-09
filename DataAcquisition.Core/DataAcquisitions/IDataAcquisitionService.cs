using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAcquisition.Core.Communication;

namespace DataAcquisition.Core.DataAcquisitions;

/// <summary>
/// Defines the contract for data acquisition operations.
/// </summary>
public interface IDataAcquisitionService : IDisposable
{
    /// <summary>
    /// 开始采集任务
    /// </summary>
    Task StartCollectionTasks();

    /// <summary>
    /// 停止采集任务
    /// </summary>
    /// <returns></returns>
    Task StopCollectionTasks();

    /// <summary>
    /// 获取 PLC 连接状态
    /// </summary>
    /// <returns></returns>
    SortedDictionary<string, bool> GetPlcConnectionStatus();

    /// <summary>
    /// 写入 PLC 寄存器
    /// </summary>
    /// <param name="plcCode">PLC 编号</param>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <param name="dataType">数据类型</param>
    /// <returns>写入结果</returns>
    Task<CommunicationWriteResult> WritePlcAsync(string plcCode, string address, object value, string dataType);
}