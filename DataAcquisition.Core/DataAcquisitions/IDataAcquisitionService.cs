using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
}