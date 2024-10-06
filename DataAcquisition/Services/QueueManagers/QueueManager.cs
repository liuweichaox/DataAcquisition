using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Services.QueueManagers;

/// <summary>
/// 队里管理器
/// </summary>
public class QueueManager : IQueueManager
{
    private readonly BlockingCollection<Dictionary<string, object>> _queue;
    private readonly IDataStorage _dataStorage;
    private readonly List<Dictionary<string, object>> _dataBatch;
    private readonly DataAcquisitionConfig _dataAcquisitionConfig;

    public QueueManager(IDataStorage dataStorage, DataAcquisitionConfig dataAcquisitionConfig)
    {
        _queue = new BlockingCollection<Dictionary<string, object>>();
        _dataStorage = dataStorage;
        _dataAcquisitionConfig = dataAcquisitionConfig;
        _dataBatch = new List<Dictionary<string, object>>();
        Task.Run(ProcessQueue);
    }

    /// <summary>
    /// 将数据和其对应的 DataAcquisitionConfig 添加到队列
    /// </summary>
    public void EnqueueData(Dictionary<string, object> data)
    {
        _queue.Add(data);
    }

    /// <summary>
    /// 处理队列，支持根据不同的 DataAcquisitionConfig 进行批量插入
    /// </summary>
    private async Task ProcessQueue()
    {
        foreach (var data in _queue.GetConsumingEnumerable())
        {
            _dataBatch.Add(data);

            if (_dataBatch.Count >= _dataAcquisitionConfig.BatchSize)
            {
                await _dataStorage.SaveBatchAsync(_dataBatch);
                _dataBatch.Clear();
            }
        }

        if (_dataBatch.Count > 0)
        {
            await _dataStorage.SaveBatchAsync(_dataBatch);
        }
    }

    /// <summary>
    /// 完成队列，防止再添加数据
    /// </summary>
    public void Complete()
    {
        _queue.CompleteAdding();
    }
}