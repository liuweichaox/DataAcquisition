using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;

namespace WebAppSamples.Services.DataStorages;

public class DataStorageFactory : IDataStorageFactory
{
    public IDataStorage Create(DataAcquisitionConfig config)
    {
        return new SqLiteDataStorage(config);
    }
}