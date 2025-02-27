using System;

namespace DataAcquisition.Core.DataStorages;

public class DataStorageFactory : IDataStorageFactory
{
    public IDataStorage Create(DataAcquisitionConfig config, string type)
    {
        return type switch
        {
            "Sqlite" => new SqLiteDataStorage(config),
            _ => throw new NotSupportedException("PLC 类型不支持")
        };
    }
}