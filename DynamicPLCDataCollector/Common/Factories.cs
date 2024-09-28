using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.PLCClients;

namespace DynamicPLCDataCollector.Common;

public delegate IPLCClient PLCClientFactory(string ipAddress, int port);

public delegate IDataStorage DataStorageFactory();