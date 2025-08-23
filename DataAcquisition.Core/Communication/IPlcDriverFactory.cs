namespace DataAcquisition.Core.Communication;

/// <summary>
/// PLC 客户端工厂
/// </summary>
public interface IPlcDriverFactory
{
    /// <summary>
    /// 创建对应协议的 PLC 客户端
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>PLC 客户端</returns>
    IPlcClient Create(DeviceConfig config);
}
