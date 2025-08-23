using System;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Profinet.Inovance;
using HslCommunication.Profinet.Melsec;

namespace DataAcquisition.Core.Communication;

public class PlcDriverFactory : IPlcDriverFactory
{
    public DeviceTcpNet Create(DeviceConfig config)
    {
        return config.DriverType switch
        {
            "MelsecA1ENet" => new MelsecA1ENet(config.Host, config.Port)
            {
                ReceiveTimeOut = 2000,
                ConnectTimeOut = 2000
            },
            "MelsecA1EAsciiNet" =>new MelsecA1EAsciiNet(config.Host, config.Port) 
            {
                ReceiveTimeOut = 2000,
                ConnectTimeOut = 2000
            },
            "InovanceTcpNet"=> new InovanceTcpNet(config.Host, config.Port)
            {
                ReceiveTimeOut = 2000,
                ConnectTimeOut = 2000,
                Station = 1,
                AddressStartWithZero = true,
                IsStringReverse = false,
                Series = InovanceSeries.AM,
                DataFormat = DataFormat.CDAB
            },
            _ => throw new ArgumentException("Unsupported plc driver type", nameof(config.DriverType))
        };
    }
}