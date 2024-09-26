using System.Collections.Concurrent;
using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.DataStorages;

/// <summary>
/// 数据存储抽象类
/// </summary>
public abstract class AbstractDataStorage : IDataStorage
{
    private readonly BlockingCollection<Dictionary<string, object>> _queue;

    public AbstractDataStorage(MetricTableConfig metricTableConfig)
    {
        _queue = new BlockingCollection<Dictionary<string, object>>();
        ProcessQueue(_queue, metricTableConfig);
    }

    public void Save(Dictionary<string, object> data, MetricTableConfig metricTableConfig)
    {
        _queue.Add(data);
    }
    
    /// <summary>
    /// 消费队列
    /// </summary>
    /// <param name="queue"></param>
    /// <param name="metricTableConfig"></param>
    protected abstract void ProcessQueue(BlockingCollection<Dictionary<string, object>> queue, MetricTableConfig metricTableConfig);
    
    public void Release()
    {
        _queue.CompleteAdding();
    }
}