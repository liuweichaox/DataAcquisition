using DataAcquisition.Core.Clients;
using DataAcquisition.Core.Models;

namespace DataAcquisition.Gateway.Infrastructure.Clients;

public class PlcClientFactory : IPlcClientFactory
{
    public IPlcClient Create(DeviceConfig config)
    {
        return new PlcClient(config);
    }
}
