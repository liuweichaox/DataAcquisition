using DataAcquisition.Core.Communication;
using DataAcquisition.Core.Models;

namespace DataAcquisition.Gateway.Infrastructure.Communication;

public class CommunicationFactory : ICommunicationFactory
{
    public ICommunicationService Create(DeviceConfig config)
    {
        return new CommunicationService(config);
    }
}
