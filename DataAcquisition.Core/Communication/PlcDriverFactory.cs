using System;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Profinet.Inovance;
using HslCommunication.Profinet.Melsec;

namespace DataAcquisition.Core.Communication;

public class PlcDriverFactory : IPlcDriverFactory
{
    public IPlcClient Create(DeviceConfig config)
    {
        return new HslPlcClient(config);
    }
}
