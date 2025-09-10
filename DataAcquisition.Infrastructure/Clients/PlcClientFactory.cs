using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.Clients;

public class PlcClientFactory : IPlcClientFactory
{
    public IPlcClientService Create(DeviceConfig config)
    {
        return new PlcClientService(config);
    }
}
