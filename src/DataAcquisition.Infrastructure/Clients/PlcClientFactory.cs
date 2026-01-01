using System;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
///     PLC 客户端工厂。
/// </summary>
public class PlcClientFactory : IPlcClientFactory
{
    /// <summary>
    ///     创建 PLC 客户端实例。
    /// </summary>
    public IPlcClientService Create(DeviceConfig config)
    {
        return config.Type switch
        {
            PlcType.Mitsubishi => new MitsubishiPlcClientService(config),
            PlcType.Inovance => new InovancePlcClientService(config),
            PlcType.BeckhoffAds => new BeckhoffAdsPlcClientService(config),
            _ => throw new NotImplementedException("不支持的 PLC 类型")
        };
    }
}