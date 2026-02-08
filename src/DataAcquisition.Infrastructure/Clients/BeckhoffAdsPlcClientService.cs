using DataAcquisition.Domain.Models;
using HslCommunication.Profinet.Beckhoff;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
///     倍福 ADS 协议 PLC 客户端。
/// </summary>
public class BeckhoffAdsPlcClientService(DeviceConfig config)
    : HslPlcClientServiceBase(CreateDevice<BeckhoffAdsNet>(config));