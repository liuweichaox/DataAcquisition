using DataAcquisition.Application.Clients;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.Clients;

public class PlcClientFactory : IPlcClientFactory
{
    public IPlcClient Create(DeviceConfig config)
    {
        return new PlcClient(config);
    }
}
