using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

public interface IModuleCollector
{
    Task CollectAsync(DeviceConfig config, Module module, IPlcClientService client, CancellationToken ct = default);
}
