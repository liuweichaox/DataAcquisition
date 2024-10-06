using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Services.DataStorages;

public abstract class AbstractDataStorage(Device device, DataAcquisitionConfig dataAcquisitionConfig) : IDataStorage
{
    protected readonly Device Device = device;
    protected readonly DataAcquisitionConfig DataAcquisitionConfig = dataAcquisitionConfig;

    public abstract Task SaveBatchAsync(List<Dictionary<string, object>> data);
    public abstract ValueTask DisposeAsync();
}