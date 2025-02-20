namespace DataAcquisition.Services.PlcClients;

/// <summary>
/// <see cref="IPlcClient"/> 工厂
/// </summary>
public interface IPlcClientFactory
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    IPlcClient Create(DataAcquisitionConfig config);
}