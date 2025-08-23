using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Models;

namespace DataAcquisition.Gateway.Infrastructure.DataStorages;

public class DataStorageFactory : IDataStorageFactory
{
    public IDataStorage Create(DeviceConfig config)
    {
        return new MySqlDataStorage(config.ConnectionString);
    }
}
