using System.Collections.Generic;
using System.Threading.Tasks;

namespace DynamicPLCDataCollector.Services.DataStorages;

public abstract class AbstractDataStorage : IDataStorage
{
    public AbstractDataStorage(Device device, MetricTableConfig metricTableConfig)
    {
    }

    public abstract Task SaveBatchAsync(List<Dictionary<string, object>> data);
    
    public abstract ValueTask DisposeAsync();
}