using HslCommunication.Core.Device;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// <see cref="DeviceTcpNet"/> 工厂
/// </summary>
public interface IPlcDriverFactory
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    DeviceTcpNet Create(DeviceConfig config);
}