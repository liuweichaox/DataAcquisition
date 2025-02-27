namespace DataAcquisition.Core.DataStorages;

/// <summary>
/// <see cref="IDataStorage"/> 工厂
/// </summary>
public interface IDataStorageFactory
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    IDataStorage Create(DataAcquisitionConfig config);
}