using DataAcquisition.Core.Communication;
using DataAcquisition.Core.Models;

namespace DataAcquisition.Gateway.Infrastructure.Communication;

public class CommunicationFactory : ICommunicationFactory
{
    public ICommunication Create(DeviceConfig config)
    {
        return new Communication(config);
    }
}
