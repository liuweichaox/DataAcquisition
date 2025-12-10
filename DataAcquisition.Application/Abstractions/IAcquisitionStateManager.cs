using System.Threading.Tasks;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 采集周期状态管理器接口，用于管理条件采集的开始和结束状态
///
/// 职责：
/// - 管理采集周期的生命周期（开始、结束、查询）
/// - 为每个采集周期生成唯一的CycleId（GUID）
/// - 维护活跃的采集周期状态（内存存储）
/// - 支持状态清理操作
///
/// 工作原理：
/// - StartCycle：生成唯一ID，创建采集周期对象并存储
/// - EndCycle：从存储中移除采集周期，返回CycleId用于Update操作
/// - GetActiveCycle：查询当前活跃的采集周期（用于调试和监控）
///
/// 注意：当前实现使用内存存储，系统重启后状态会丢失。如需持久化，可以实现基于数据库的版本。
/// </summary>
public interface IAcquisitionStateManager
{
    /// <summary>
    /// 开始一个新的采集周期
    /// </summary>
    /// <param name="deviceCode">设备编码</param>
    /// <param name="measurement">测量值名称（Measurement）</param>
    /// <returns>采集周期对象，包含生成的CycleId</returns>
    AcquisitionCycle StartCycle(string deviceCode, string measurement);

    /// <summary>
    /// 结束一个采集周期
    /// </summary>
    /// <param name="deviceCode">设备编码</param>
    /// <param name="measurement">测量值名称（Measurement）</param>
    /// <returns>采集周期对象，如果不存在则返回null</returns>
    AcquisitionCycle? EndCycle(string deviceCode, string measurement);

    /// <summary>
    /// 获取当前活跃的采集周期
    /// </summary>
    /// <param name="deviceCode">设备编码</param>
    /// <param name="measurement">测量值名称（Measurement）</param>
    /// <returns>采集周期对象，如果不存在则返回null</returns>
    AcquisitionCycle? GetActiveCycle(string deviceCode, string measurement);

    /// <summary>
    /// 清理指定设备的所有采集周期状态
    /// </summary>
    /// <param name="deviceCode">设备编码</param>
    void ClearCycles(string deviceCode);

    /// <summary>
    /// 清理所有采集周期状态
    /// </summary>
    void ClearAllCycles();
}
