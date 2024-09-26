using System.Collections.Concurrent;
using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.DataStorages;

public abstract class AbstractDataStorage : IDataStorage
{
    private readonly ConcurrentDictionary<string, BlockingCollection<Dictionary<string, object>>> _queueDictionary = new();

    public void Save(Dictionary<string, object> data, MetricTableConfig metricTableConfig)
    {
        var queue = _queueDictionary.GetOrAdd(metricTableConfig.TableName, tableName =>
        {
            var newQueue = new BlockingCollection<Dictionary<string, object>>();
            
            ProcessQueue(newQueue, metricTableConfig);
            
            return newQueue;
        });

        queue.Add(data);
    }
    protected abstract void ProcessQueue(BlockingCollection<Dictionary<string, object>> queue, MetricTableConfig metricTableConfig);
    
    public void ReleaseAll()
    {
        foreach (var queue in _queueDictionary.Values)
        {
            queue.CompleteAdding();
        }
    }
}