# 🔄 Data Processing Flow

This document details the data processing flow of the DataAcquisition system.

## Related Documents

- [Design Philosophy](design.en.md) - Understand system design philosophy
- [Core Module Documentation](modules.en.md) - Understand system core modules

## Normal Flow

1. **Data Acquisition**: ChannelCollector reads data from PLC
2. **Queue Aggregation**: LocalQueueService aggregates data by BatchSize
3. **WAL Write**: Immediately write to Parquet file as write-ahead log
4. **Primary Storage Write**: Immediately write to InfluxDB
5. **WAL Cleanup**: Delete corresponding Parquet file on successful write

## Exception Handling Flow

### Network Failures

- **Automatic Reconnection Mechanism**: System automatically detects PLC connection status and automatically reconnects after disconnection
- **Heartbeat Monitoring**: Monitor PLC connection status through heartbeat registers
- **Connection Status Recording**: Record connection status changes for troubleshooting

### Storage Failures

- **WAL File Retention**: Parquet WAL files are retained when InfluxDB write fails
- **Automatic Retry**: Retry writes periodically by ParquetRetryWorker
- **Retry Strategy**: Supports configuration of retry interval and maximum retry count

### Configuration Errors

- **Configuration Validation**: Validate configuration file format and completeness at startup
- **Hot Reload Mechanism**: Use FileSystemWatcher to monitor configuration file changes, supporting hot updates
- **Error Logging**: Record detailed logs on configuration errors for troubleshooting

## Data Flow Diagram

```
PLC Device
    ↓
ChannelCollector (Acquisition)
    ↓
LocalQueueService (Queue Aggregation)
    ↓
    ├─→ ParquetFileStorageService (WAL Write)
    │       ↓
    │   Write Success → Delete WAL File
    │   Write Failure → Retain WAL File → RetryWorker Retry
    │
    └─→ InfluxDbDataStorageService (Primary Storage Write)
            ↓
         Write Success → Complete
         Write Failure → Retain WAL File → RetryWorker Retry
```

## Data Consistency Guarantees

- **WAL-first Architecture**: All data is written to local Parquet files first, ensuring no data loss
- **Atomic Operations**: Batch writes are either all successful or all failed
- **Idempotency**: Retry mechanism ensures duplicate writes do not cause data duplication

> For performance optimization recommendations, please refer to [Performance Optimization Guide](performance.en.md)

## Next Steps

After understanding the data processing flow, we recommend continuing to learn:

- Read [Design Philosophy](design.en.md) to understand system design philosophy
