using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Services.DataStorages;

public abstract class AbstractDataStorage(DataAcquisitionConfig config) : IDataStorage
{
    protected readonly DataAcquisitionConfig DataAcquisitionConfig = config;
    public abstract Task SaveBatchAsync(List<Dictionary<string, object>> data);
    public abstract ValueTask DisposeAsync();
}