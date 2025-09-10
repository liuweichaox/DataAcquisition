using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
/// PLC 客户端工厂。
/// </summary>
public class PlcClientFactory : IPlcClientFactory
{
    /// <summary>
    /// 创建 PLC 客户端实例。
    /// </summary>
    public IPlcClientService Create(DeviceConfig config)
    {
        return new PlcClientService(config);
    }
}
