using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Services.DataStorages;

public abstract class AbstractDataStorage(Device device, DataAcquisitionConfig dataAcquisitionConfig) : IDataStorage
{
    protected readonly Device _device = device;
    protected readonly DataAcquisitionConfig _dataAcquisitionConfig = dataAcquisitionConfig;

    public abstract Task SaveBatchAsync(List<Dictionary<string, object>> data);
    public abstract ValueTask DisposeAsync();
}