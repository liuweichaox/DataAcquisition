# 🔄 Data Processing Flow

This document details the data processing flow of the DataAcquisition system.

## Overview

This page focuses on data flow and WAL behavior. Use the index for full navigation.

## Normal Flow

1. **Data Acquisition**: ChannelCollector reads data from PLC
2. **Queue Aggregation**: QueueService aggregates data by BatchSize
3. **WAL Write**: Immediately write to Parquet file as write-ahead log
4. **Primary Storage Write**: Immediately write to InfluxDB
5. **WAL Cleanup**: Delete corresponding Parquet file on successful write

## Exception Handling Flow

### Network Failures

- **Automatic Reconnection Mechanism**: System automatically detects PLC connection status and automatically reconnects after disconnection
- **Heartbeat Monitoring**: Monitor PLC connection status through heartbeat registers
- **Connection Status Recording**: Record connection status changes for troubleshooting

### Storage Failures

- **WAL File Movement**: When InfluxDB write fails, Parquet WAL files are moved from `pending` folder to `retry` folder
- **Automatic Retry**: ParquetRetryWorker periodically scans `retry` folder and retries writes
- **Folder Isolation**: Uses two folders (`pending` and `retry`) for complete isolation, avoiding concurrency conflicts
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
QueueService (Queue Aggregation)
    ↓
    ├─→ ParquetFileStorageService (WAL Write to pending folder)
    │       ↓
    │   Write Success → Delete WAL File
    │   Write Failure → Move to retry folder → RetryWorker Retry
    │
    └─→ InfluxDbDataStorageService (Primary Storage Write)
            ↓
         Write Success → Complete
         Write Failure → Move WAL File to retry folder → RetryWorker Retry
```

## Data Consistency and Recoverability

- **WAL-first architecture**: healthy messages are written to local Parquet WAL before primary storage is attempted
- **Batch degradation**: if batch WAL persistence fails, the system degrades to per-message handling to preserve healthy messages
- **Poison-message audit**: messages that still cannot enter WAL are quarantined under `invalid/`
- **Retry replay**: files under `retry/` are replayed by the background worker to restore primary-store consistency

> For performance optimization recommendations, please refer to [Performance Optimization Guide](performance.en.md)

## Next Steps

After understanding the data processing flow, continue with:

- [Documentation Index](index.en.md)
- [Design](design.en.md)
- [Performance Recommendations](performance.en.md)
