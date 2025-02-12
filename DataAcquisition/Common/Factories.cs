using System.Collections.Generic;

namespace DataAcquisition.Common;

public delegate IPlcClient PlcClientFactory(string ipAddress, int port);
public delegate IDataStorage DataStorageFactory(DataAcquisitionConfig dataAcquisitionConfig);
public delegate void ProcessReadData(Dictionary<string, object> data, DataAcquisitionConfig config);