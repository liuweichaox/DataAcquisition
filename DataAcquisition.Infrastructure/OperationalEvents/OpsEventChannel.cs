using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.OperationalEvents;

namespace DataAcquisition.Infrastructure.OperationalEvents;

/// <summary>
/// 使用内存通道实现的运行事件总线。
/// </summary>
public sealed class OpsEventChannel : IOpsEventBus
{
    private readonly Channel<OpsEvent> _channel = Channel.CreateUnbounded<OpsEvent>();

    /// <summary>
    /// 发布事件到内存通道。
    /// </summary>
    public ValueTask PublishAsync(OpsEvent evt, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(evt, ct);

    /// <summary>
    /// 用于消费事件的读取器。
    /// </summary>
    public ChannelReader<OpsEvent> Reader => _channel.Reader;
}
