using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 模块数据采集器接口。
/// </summary>
public interface IModuleCollector
{
    /// <summary>
    /// 执行模块数据采集。
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="module">采集模块</param>
    /// <param name="client">PLC 通讯客户端</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task CollectAsync(DeviceConfig config, Module module, IPlcClientService client, CancellationToken ct = default);
}
