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
    /// 开始一个新的采集周期。
    ///
    /// 功能说明：
    /// - 为指定的设备和测量值创建一个新的采集周期
    /// - 生成唯一的 CycleId（GUID），用于关联 Start 和 End 事件
    /// - 记录周期的开始时间和相关元数据
    ///
    /// 状态管理：
    /// - 使用复合键（plcCode:measurement）存储周期状态
    /// - 如果已存在活跃周期，会先移除旧的周期（处理异常情况，如系统重启）
    /// - 同一设备的多个测量值可以同时进行条件采集（独立周期）
    ///
    /// 线程安全：
    /// - 使用 ConcurrentDictionary 保证线程安全
    /// - 支持多线程并发调用
    ///
    /// 使用场景：
    /// - 条件采集模式下的 Start 事件触发时调用
    /// - 每个 Start 事件对应一个采集周期，直到对应的 End 事件结束
    /// </summary>
    /// <param name="plcCode">PLC 设备编码（PLCCode）</param>
    /// <param name="measurement">测量值名称（Measurement），用于区分不同的数据表</param>
    /// <param name="channelCode">通道编码（ChannelCode），用于标识采集通道</param>
    /// <returns>新创建的采集周期对象，包含生成的 CycleId 和开始时间</returns>
    public AcquisitionCycle StartCycle(string plcCode, string measurement, string channelCode)
    {
        var key = GetKey(plcCode, measurement);
        var cycle = new AcquisitionCycle
        {
            CycleId = Guid.NewGuid().ToString(),
            StartTime = DateTime.Now,
            Measurement = measurement,
            PLCCode = plcCode,
            ChannelCode = channelCode
        };

        // 如果已存在活跃周期，先移除旧的（处理异常情况）
        _activeCycles.TryRemove(key, out _);
        _activeCycles.TryAdd(key, cycle);

        return cycle;
    }

    /// <summary>
    /// 结束一个采集周期。
    ///
    /// 功能说明：
    /// - 从活跃周期集合中移除指定的采集周期
    /// - 返回已结束的周期对象，包含 CycleId 等信息
    /// - 如果周期不存在，返回 null
    ///
    /// 状态清理：
    /// - 周期结束后，从内存中移除，释放资源
    /// - 周期的 CycleId 可以用于 End 事件的数据关联
    ///
    /// 使用场景：
    /// - 条件采集模式下的 End 事件触发时调用
    /// - 与 StartCycle 配对使用，形成完整的采集周期生命周期
    ///
    /// 注意事项：
    /// - 如果对应的周期不存在（可能已被清理或从未开始），返回 null
    /// - 调用方应该检查返回值，处理周期不存在的情况
    /// </summary>
    /// <param name="plcCode">PLC 设备编码（PLCCode）</param>
    /// <param name="measurement">测量值名称（Measurement），与 StartCycle 保持一致</param>
    /// <returns>已结束的采集周期对象，如果不存在则返回 null</returns>
    public AcquisitionCycle? EndCycle(string plcCode, string measurement)
    {
        var key = GetKey(plcCode, measurement);
        if (_activeCycles.TryRemove(key, out var cycle))
        {
            return cycle;
        }

        return null;
    }

    /// <summary>
    /// 获取当前活跃的采集周期
    /// </summary>
    public AcquisitionCycle? GetActiveCycle(string plcCode, string measurement)
    {
        var key = GetKey(plcCode, measurement);
        return _activeCycles.TryGetValue(key, out var cycle) ? cycle : null;
    }

    /// <summary>
    /// 清理指定设备的所有采集周期状态
    /// </summary>
    public void ClearCycles(string plcCode)
    {
        var keysToRemove = new List<string>();
        foreach (var kvp in _activeCycles)
        {
            if (kvp.Value.PLCCode == plcCode)
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
    /// <param name="plcCode">PLC编码（PLCCode）</param>
    private static string GetKey(string plcCode, string measurement)
    {
        return $"{plcCode}:{measurement}";
    }
}
