using HslCommunication.Core.Device;
using HslCommunication.Profinet.Melsec;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// PLC 客户端实现
/// </summary>
public class MelsecA1ENetPlcDriver(DataAcquisitionConfig config) : PlcDriverBase
{
    public override DeviceTcpNet CreateDeviceTcpNet()
    {
        var melsecA1ENet = new MelsecA1ENet(config.Plc.IpAddress, config.Plc.Port)
        {
            ReceiveTimeOut = 2000,
            ConnectTimeOut = 2000
        };
        return melsecA1ENet;
    }
}