using DataAcquisition.Core.Communication;
using DataAcquisition.Core.DeviceConfigs;

namespace DataAcquisition.Gateway.Communication;

public class CommunicationFactory : ICommunicationFactory
{
    public ICommunication Create(DeviceConfig config)
    {
        return new Communication(config);
    }
}
