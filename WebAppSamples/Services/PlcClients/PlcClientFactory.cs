using DataAcquisition.Models;
using DataAcquisition.Services.PlcClients;

namespace WebAppSamples.Services.PlcClients;

public class PlcClientFactory : IPlcClientFactory
{
    public IPlcClient Create(DataAcquisitionConfig config)
    {
        return new PlcClient(config);
    }
}