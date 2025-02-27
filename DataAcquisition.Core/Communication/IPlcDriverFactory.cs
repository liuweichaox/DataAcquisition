namespace DataAcquisition.Core.Communication;

/// <summary>
/// <see cref="IPlcDriver"/> 工厂
/// </summary>
public interface IPlcDriverFactory
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="config"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    IPlcDriver Create(DataAcquisitionConfig config, string type);
}