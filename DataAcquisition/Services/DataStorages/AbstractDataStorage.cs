using System.Collections.Generic;
using System.Threading.Tasks;
using DataAcquisition.Models;

namespace DataAcquisition.Services.DataStorages;

public abstract class AbstractDataStorage : IDataStorage
{
    public AbstractDataStorage(Device device, DataAcquisitionConfig dataAcquisitionConfig)
    {
    }

    public abstract Task SaveBatchAsync(List<Dictionary<string, object>> data);
    
    public abstract ValueTask DisposeAsync();
}