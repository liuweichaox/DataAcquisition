using System;

namespace DataAcquisition.Core.DataStorages;

public class DataStorageFactory : IDataStorageFactory
{
    public IDataStorage Create(DeviceConfig config)
    {
        return config.StorageType switch
        {
            "MySQL" => new MySqlDataStorage(config.ConnectionString),
            _ => throw new ArgumentException("Unsupported storage type", nameof(config.StorageType))
        };
    }
}