namespace DataAcquisition.Core.Queues;

/// <summary>
/// 队列工厂接口
/// </summary>
public interface IQueueFactory
{
    IQueueService Create(DeviceConfig config);
}
