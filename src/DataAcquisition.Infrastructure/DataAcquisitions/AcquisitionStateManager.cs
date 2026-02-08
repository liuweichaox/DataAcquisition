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

    public AcquisitionCycle StartCycle(string plcCode, string channelCode, string measurement)
    {
        var key = $"{plcCode}:{channelCode}:{measurement}";
        var cycle = new AcquisitionCycle
        {
            CycleId = Guid.NewGuid().ToString(),
            Measurement = measurement,
            PlcCode = plcCode,
            ChannelCode = channelCode
        };
        _activeCycles[key] = cycle;
        return cycle;
    }

    public AcquisitionCycle? EndCycle(string plcCode, string channelCode, string measurement)
    {
        var key = $"{plcCode}:{channelCode}:{measurement}";
        return _activeCycles.TryRemove(key, out var cycle) ? cycle : null;
    }
}