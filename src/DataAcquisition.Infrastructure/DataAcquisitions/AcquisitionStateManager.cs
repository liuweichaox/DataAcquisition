using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
///     采集周期状态管理器。使用复合键（plcCode:channelCode:measurement）存储活跃周期，支持同一设备的多个并发采集。
/// </summary>
public class AcquisitionStateManager : IAcquisitionStateManager
{
    private readonly ConcurrentDictionary<string, AcquisitionCycle> _activeCycles = new();

    /// <summary>
    ///     启动采集周期。生成唯一的 CycleId，并存储到活跃周期集合中。若已存在相同键的周期，则移除旧周期。
    /// </summary>
    public AcquisitionCycle StartCycle(string plcCode, string channelCode, string measurement)
    {
        var key = GetKey(plcCode, channelCode, measurement);
        var cycle = new AcquisitionCycle
        {
            CycleId = Guid.NewGuid().ToString(),
            Measurement = measurement,
            PlcCode = plcCode,
            ChannelCode = channelCode
        };

        _activeCycles.TryRemove(key, out _);
        _activeCycles.TryAdd(key, cycle);

        return cycle;
    }

    /// <summary>
    ///     结束采集周期。从活跃周期集合中移除并返回指定周期；若不存在则返回 null。
    /// </summary>
    public AcquisitionCycle? EndCycle(string plcCode, string channelCode, string measurement)
    {
        var key = GetKey(plcCode, channelCode, measurement);
        return _activeCycles.TryRemove(key, out var cycle) ? cycle : null;
    }
    
    /// <summary>
    ///     生成复合键：plcCode:channelCode:measurement
    /// </summary>
    private static string GetKey(string plcCode, string channelCode, string measurement)
    {
        return $"{plcCode}:{channelCode}:{measurement}";
    }
}