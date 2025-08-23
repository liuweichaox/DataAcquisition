namespace DataAcquisition.Core.DataStorages;

public interface IDataStorageFactory
{
    IDataStorageService Create(DeviceConfig config);
}