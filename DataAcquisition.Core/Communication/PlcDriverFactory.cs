using System;

namespace DataAcquisition.Core.Communication;

public class PlcDriverFactory : IPlcDriverFactory
{
    public IPlcDriver Create(DataAcquisitionConfig config, string type)
    {
        return type switch
        {
            "MelsecA1ENet" => new MelsecA1ENetPlcDriver(config),
            _ => throw new NotSupportedException("PLC 类型不支持")
        };
    }
}