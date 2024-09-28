namespace DynamicPLCDataCollector.Common;

public delegate IPLCClient PLCClientFactory(string ipAddress, int port);
public delegate IDataStorage DataStorageFactory(Device device,MetricTableConfig metricTableConfig);