using System;

namespace DataAcquisition.Domain.Models;

/// <summary>
/// 采集周期状态，用于管理条件采集的开始和结束状态
/// </summary>
public class AcquisitionCycle
{
    /// <summary>
    /// 采集周期唯一标识符（GUID）
    /// </summary>
    public string CycleId { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 设备编码
    /// </summary>
    public string DeviceCode { get; set; }

    /// <summary>
    /// 通道名称
    /// </summary>
    public string ChannelName { get; set; }
}
