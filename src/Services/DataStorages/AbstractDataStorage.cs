namespace DynamicPLCDataCollector.Services.DataStorages;

public abstract class AbstractDataStorage : IDataStorage
{
    public AbstractDataStorage(Device device, MetricTableConfig metricTableConfig)
    {
    }

    public abstract ValueTask DisposeAsync();
}