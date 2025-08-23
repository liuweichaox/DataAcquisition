namespace DataAcquisition.Core.Queues;

/// <summary>
/// 队列工厂接口
/// </summary>
public interface IQueueFactory
{
    IQueue Create(DeviceConfig config);
}
