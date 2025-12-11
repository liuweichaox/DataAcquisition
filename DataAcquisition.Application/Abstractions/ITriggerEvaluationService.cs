using DataAcquisition.Domain.Models;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 触发条件评估服务接口，用于判断是否满足触发条件
///
/// 职责：
/// - 根据触发模式（TriggerMode）判断是否应该触发采集
/// - 支持多种触发模式：Always、RisingEdge、FallingEdge、ValueIncrease、ValueDecrease
/// - 通过比较前一个值和当前值来判断状态变化
///
/// 典型使用场景：
/// - RisingEdge：设备启动信号（从0变1）
/// - FallingEdge：设备停止信号（从1变0）
/// - ValueIncrease：批次号增加
/// - ValueDecrease：批次号减少
/// </summary>
public interface ITriggerEvaluationService
{
    /// <summary>
    /// 判断是否应该触发采集
    /// </summary>
    /// <param name="mode">触发模式：RisingEdge（上升沿）、FallingEdge（下降沿）、ValueIncrease（值增加）、ValueDecrease（值减少）</param>
    /// <param name="previousValue">前一个读取的值，用于比较状态变化</param>
    /// <param name="currentValue">当前读取的值，用于比较状态变化</param>
    /// <returns>如果应该触发采集则返回true，否则返回false。如果previousValue或currentValue为null（首次读取），默认返回true</returns>
    bool ShouldTrigger(AcquisitionTrigger? mode, object? previousValue, object? currentValue);
}
