using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Models;

namespace DataAcquisition.Gateway.Infrastructure.DataStorages;

public class DataStorageFactory : IDataStorageFactory
{
    public IDataStorageService Create(DeviceConfig config)
    {
        return new MySqlDataStorageService(config.ConnectionString);
    }
}
