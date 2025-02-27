using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Core.DataStorages;

public abstract class AbstractDataStorage(DataAcquisitionConfig config) : IDataStorage
{
    public abstract Task SaveAsync(DataPoint dataPoint);
    public abstract Task SaveBatchAsync(List<DataPoint> dataPoints);
    public abstract void Dispose();
}