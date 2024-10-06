using System.Collections.Generic;

namespace DataAcquisition.Common;

public delegate IPLCClient PLCClientFactory(string ipAddress, int port);
public delegate IDataStorage DataStorageFactory(Device device, DataAcquisitionConfig dataAcquisitionConfig);
public delegate void ProcessReadData(Dictionary<string, object> data, Device device);