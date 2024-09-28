using System.Collections.Concurrent;
using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.Services;

/// <summary>
/// 队里管理器
/// </summary>
public class QueueManager : IQueueManager
{
    private readonly BlockingCollection<Dictionary<string, object>> _queue;
    private readonly IDataStorage _dataStorage;
    private readonly List<Dictionary<string, object>> _dataBatch;
    private readonly MetricTableConfig _metricTableConfig;
    public QueueManager(IDataStorage dataStorage, MetricTableConfig metricTableConfig)
    {
        _queue = new BlockingCollection<Dictionary<string, object>>();
        _dataStorage = dataStorage;
        _metricTableConfig = metricTableConfig;
        _dataBatch = new List<Dictionary<string, object>>();
        Task.Run(ProcessQueue);
    }

    /// <summary>
    /// 将数据和其对应的 MetricTableConfig 添加到队列
    /// </summary>
    public void EnqueueData(Dictionary<string, object> data)
    {
        _queue.Add(data);
    }

    /// <summary>
    /// 处理队列，支持根据不同的 MetricTableConfig 进行批量插入
    /// </summary>
    private async Task ProcessQueue()
    {
        foreach (var data in _queue.GetConsumingEnumerable())
        {
            _dataBatch.Add(data);
            
            if (_dataBatch.Count >= _metricTableConfig.BatchSize)
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