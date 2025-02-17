using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Services.DataStorages;

public abstract class AbstractDataStorage(DataAcquisitionConfig config) : IDataStorage
{
    public abstract Task SaveAsync(DataPoint? dataPoint);
    public abstract Task SaveBatchAsync(List<DataPoint?> dataPoints);
    public abstract ValueTask DisposeAsync();
}