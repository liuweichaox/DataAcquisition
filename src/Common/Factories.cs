using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.Services.DataStorages;
using DynamicPLCDataCollector.Services.PLCClients;

namespace DynamicPLCDataCollector.Common;

public delegate IPLCClient PLCClientFactory(string ipAddress, int port);

public delegate IDataStorage DataStorageFactory(MetricTableConfig metricTableConfig);