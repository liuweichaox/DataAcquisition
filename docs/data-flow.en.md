# 🔄 Data Processing Flow

This document details the data processing flow of the DataAcquisition system.

## Normal Flow

1. **Data Acquisition**: ChannelCollector reads data from PLC
2. **Queue Aggregation**: LocalQueueService aggregates data by BatchSize
3. **WAL Write**: Immediately write to Parquet file as write-ahead log
4. **Primary Storage Write**: Immediately write to InfluxDB
5. **WAL Cleanup**: Delete corresponding Parquet file on successful write

## Exception Handling

- **Network Failures**: Automatic reconnection mechanism
- **Storage Failures**: WAL files retained, retried by ParquetRetryWorker
- **Configuration Errors**: Configuration validation and hot reload mechanism

For detailed flow documentation, see: [Chinese Data Flow Guide](data-flow.md)
