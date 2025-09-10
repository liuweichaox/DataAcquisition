using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

public interface IHeartbeatMonitor
{
    Task MonitorAsync(DeviceConfig config, CancellationToken ct = default);
}
