namespace DataAcquisition.Services.QueueManagers;

/// <summary>
/// <see cref="IQueueManager"/> 工厂
/// </summary>
public interface IQueueManagerFactory
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="dataStorage"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    IQueueManager Create(IDataStorage dataStorage, DataAcquisitionConfig config);
}