using DataAcquisition.Domain.Models;
using HslCommunication.Profinet.Inovance;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
///     汇川 PLC 客户端。
/// </summary>
public class InovancePlcClientService(DeviceConfig config)
    : HslPlcClientServiceBase(CreateDevice<InovanceTcpNet>(config));