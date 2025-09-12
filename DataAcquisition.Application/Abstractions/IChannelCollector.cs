using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 通道数据采集器接口。
/// </summary>
public interface IChannelCollector
{
    /// <summary>
    /// 执行通道数据采集。
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="channel">采集通道</param>
    /// <param name="client">PLC 通讯客户端</param>
    /// <param name="ct">可选的取消标记</param>
    /// <returns>表示异步操作的任务。</returns>
    Task CollectAsync(DeviceConfig config, Channel channel, IPlcClientService client, CancellationToken ct = default);
}
