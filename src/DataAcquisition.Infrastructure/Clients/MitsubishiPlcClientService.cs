using DataAcquisition.Domain.Models;
using HslCommunication.Profinet.Melsec;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
///     三菱 PLC 客户端（A1E 协议）。
/// </summary>
public class MitsubishiPlcClientService(DeviceConfig config)
    : HslPlcClientServiceBase(CreateDevice<MelsecA1ENet>(config));