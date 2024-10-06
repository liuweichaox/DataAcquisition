using System.Collections.Generic;
using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.PLCClients;

namespace DataAcquisition.Common;

public delegate IPLCClient PLCClientFactory(string ipAddress, int port);
public delegate IDataStorage DataStorageFactory(Device device,MetricTableConfig metricTableConfig);
public delegate void ProcessReadData(Dictionary<string, object> data, Device device);