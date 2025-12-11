using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
/// 采集周期状态管理器实现
///
/// 实现特点：
/// - 使用内存存储（ConcurrentDictionary），线程安全
/// - 使用复合键（plcCode:tableName）区分不同设备的采集周期
/// - 支持同一设备的多个表同时进行条件采集
/// - 如果StartCycle时已存在活跃周期，会先移除旧的（处理异常情况）
///
/// 状态存储：
/// - 当前实现使用内存存储，系统重启后状态会丢失
/// - 如需持久化，可以实现基于数据库的版本，支持系统重启后恢复状态
/// </summary>
public class AcquisitionStateManager : IAcquisitionStateManager
{
    // 使用复合键：plcCode:tableName -> AcquisitionCycle
    // 支持同一设备的多个表同时进行条件采集
    private readonly ConcurrentDictionary<string, AcquisitionCycle> _activeCycles = new();

    /// <summary>
    /// 开始一个新的采集周期
    /// </summary>
    public AcquisitionCycle StartCycle(string deviceCode, string measurement, string channelCode)
    {
        var key = GetKey(deviceCode, measurement);
        var cycle = new AcquisitionCycle
        {
            CycleId = Guid.NewGuid().ToString(),
            StartTime = DateTime.Now,
            Measurement = measurement,
            PLCCode = deviceCode,
            ChannelCode = channelCode
        };

        // 如果已存在活跃周期，先移除旧的（处理异常情况）
        _activeCycles.TryRemove(key, out _);
        _activeCycles.TryAdd(key, cycle);

        return cycle;
    }

    /// <summary>
    /// 结束一个采集周期
    /// </summary>
    public AcquisitionCycle? EndCycle(string deviceCode, string measurement)
    {
        var key = GetKey(deviceCode, measurement);
        if (_activeCycles.TryRemove(key, out var cycle))
        {
            return cycle;
        }

        return null;
    }

    /// <summary>
    /// 获取当前活跃的采集周期
    /// </summary>
    public AcquisitionCycle? GetActiveCycle(string deviceCode, string measurement)
    {
        var key = GetKey(deviceCode, measurement);
        return _activeCycles.TryGetValue(key, out var cycle) ? cycle : null;
    }

    /// <summary>
    /// 清理指定设备的所有采集周期状态
    /// </summary>
    public void ClearCycles(string deviceCode)
    {
        var keysToRemove = new List<string>();
        foreach (var kvp in _activeCycles)
        {
            if (kvp.Value.PLCCode == deviceCode)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _activeCycles.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 清理所有采集周期状态
    /// </summary>
    public void ClearAllCycles()
    {
        _activeCycles.Clear();
    }

    /// <summary>
    /// 生成复合键
    /// </summary>
    /// <param name="deviceCode">PLC编码（PLCCode）</param>
    private static string GetKey(string deviceCode, string measurement)
    {
        return $"{deviceCode}:{measurement}";
    }
}
