namespace DataAcquisition.Core.DataStorages;

public interface IDataStorageFactory
{
    IDataStorage Create(DeviceConfig config);
}