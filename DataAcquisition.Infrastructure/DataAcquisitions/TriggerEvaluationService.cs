using System;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
/// 触发条件评估器实现
///
/// 实现逻辑：
/// - Always：总是返回true（无条件采集）
/// - RisingEdge：前值=0且当前值=1（上升沿触发）
/// - FallingEdge：前值=1且当前值=0（下降沿触发）
/// - ValueIncrease：前值 < 当前值（值增加）
/// - ValueDecrease：前值 > 当前值（值减少）
///
/// 首次读取处理：如果previousValue或currentValue为null，默认返回true（触发采集）
/// </summary>
public class TriggerEvaluationService : ITriggerEvaluationService
{
    /// <summary>
    /// 判断是否应该触发采集
    /// </summary>
    public bool ShouldTrigger(TriggerMode mode, object? previousValue, object? currentValue)
    {
        // 如果前一个值或当前值为null，默认触发（首次读取）
        if (previousValue == null || currentValue == null)
        {
            return true;
        }

        var prev = Convert.ToDecimal(previousValue);
        var curr = Convert.ToDecimal(currentValue);

        return mode switch
        {
            TriggerMode.Always => true,
            TriggerMode.ValueIncrease => prev < curr,
            TriggerMode.ValueDecrease => prev > curr,
            TriggerMode.RisingEdge => prev == 0 && curr == 1,
            TriggerMode.FallingEdge => prev == 1 && curr == 0,
            _ => false
        };
    }
}
