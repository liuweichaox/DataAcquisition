using DataAcquisition.Core.DataStorages;

namespace DataAcquisition.Core.QueueManagers;

/// <summary>
/// 队列管理器工厂接口
/// </summary>
public interface IQueueManagerFactory
{
    IQueueManager Create(DeviceConfig config);
}