using System;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
///     PLC 客户端工厂。
/// </summary>
public class PLCClientFactory : IPLCClientFactory
{
    /// <summary>
    ///     创建 PLC 客户端实例。
    /// </summary>
    public IPLCClientService Create(DeviceConfig config)
    {
        return config.Type switch
        {
            PLCType.Mitsubishi => new MitsubishiPLCClientService(config),
            PLCType.Inovance => new InovancePLCClientService(config),
            PLCType.BeckhoffAds => new BeckhoffAdsPLCClientService(config),
            _ => throw new NotImplementedException("不支持的 PLC 类型")
        };
    }
}