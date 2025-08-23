using DataAcquisition.Core.DeviceConfigs;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 通讯客户端工厂
/// </summary>
public interface ICommunicationFactory
{
    /// <summary>
    /// 创建对应协议的通讯客户端
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>通讯客户端</returns>
    ICommunication Create(DeviceConfig config);
}
