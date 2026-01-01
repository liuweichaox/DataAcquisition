# 📊 Core Module Documentation

This document introduces the core module design and usage of the DataAcquisition system.

## Related Documents

- [Getting Started Guide](getting-started.en.md) - Get started from scratch
- [Configuration Guide](configuration.en.md) - Detailed configuration options
- [API Usage Examples](api-usage.en.md) - API interface usage methods
- [Performance Optimization Recommendations](performance.en.md) - Optimize system performance

## PLC Client Implementations

The system supports multiple PLC protocols, each with corresponding client implementations:

| Protocol     | Implementation Class          | Description                         |
| ------------ | ----------------------------- | ----------------------------------- |
| Mitsubishi   | `MitsubishiPlcClientService`  | Mitsubishi PLC communication client |
| Inovance     | `InovancePlcClientService`    | Inovance PLC communication client   |
| BeckhoffAds  | `BeckhoffAdsPlcClientService` | Beckhoff ADS protocol client        |

## ChannelCollector - Channel Collector

`ChannelCollector` is the core acquisition component of the system, responsible for reading data from PLC.

### Features

- **Automatic Connection Management**: Automatically detects and handles PLC connection status, automatically reconnects after disconnection
- **Multiple Acquisition Modes**: Supports continuous acquisition (Always) and conditional trigger acquisition (Conditional)
- **Batch Read Optimization**: Supports batch reading of multiple consecutive registers to reduce network round trips and improve acquisition efficiency
- **Thread Safety**: Ensures safe concurrent access to the same PLC device

### Acquisition Mode Description

#### Always Mode (Continuous Acquisition)

Continuously acquires data at the configured `AcquisitionInterval` interval.

**Use Cases**:
- Sensor data that needs continuous monitoring (temperature, pressure, etc.)
- Data that needs fixed frequency acquisition

**Configuration Example**:
```json
{
  "AcquisitionMode": "Always",
  "AcquisitionInterval": 100,
  "Metrics": [
    {
      "MetricName": "temperature",
      "FieldName": "temperature",
      "Register": "D6000",
      "Index": 0,
      "DataType": "short",
      "EvalExpression": "value / 100.0"
    }
  ]
}
```

#### Conditional Mode (Conditional Trigger Acquisition)

Triggers acquisition based on value changes of a specified register.

**Use Cases**:
- Production cycle management (production start/end events)
- Equipment status change recording
- Conditionally triggered data acquisition

**Configuration Example**:
```json
{
  "AcquisitionMode": "Conditional",
  "Metrics": null,
  "ConditionalAcquisition": {
    "Register": "D6006",
    "DataType": "short",
    "StartTriggerMode": "RisingEdge",
    "EndTriggerMode": "FallingEdge"
  }
}
```

## InfluxDbDataStorageService - Data Storage Service

`InfluxDbDataStorageService` is responsible for writing acquired data to InfluxDB time-series database.

### Features

- **Batch Writes**: Writes data in batches according to configured `BatchSize`, improving write efficiency
- **Automatic Retry**: Automatically retains WAL files on write failures, automatically retried by background retry mechanism
- **Performance Monitoring**: Automatically records write latency and batch efficiency metrics for performance analysis
- **Data Safety**: Uses WAL-first architecture to ensure zero data loss

### Configuration

Configure InfluxDB connection information in `appsettings.json`:

```json
{
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token-here",
    "Bucket": "plc_data",
    "Org": "your-org"
  }
}
```

**Configuration Items**:
- `Url`: InfluxDB server address
- `Token`: InfluxDB authentication token (recommended to use environment variables)
- `Bucket`: Data storage bucket name
- `Org`: InfluxDB organization name

## MetricsCollector - Metrics Collector

The system has built-in complete monitoring metrics, exposed through Prometheus format.

### Acquisition Metrics

- **`data_acquisition_collection_latency_ms`** - Collection latency (time from PLC read to database write, milliseconds)
- **`data_acquisition_collection_rate`** - Collection rate (data points per second, points/s)

### Queue Metrics

- **`data_acquisition_queue_depth`** - Queue depth (Channel pending reads + total accumulated pending messages)
- **`data_acquisition_processing_latency_ms`** - Processing latency (queue processing delay, milliseconds)

### Storage Metrics

- **`data_acquisition_write_latency_ms`** - Write latency (database write delay, milliseconds)
- **`data_acquisition_batch_write_efficiency`** - Batch write efficiency (batch size / write time, points/ms)

### Error and Connection Metrics

- **`data_acquisition_errors_total`** - Total errors (statistics by device/channel)
- **`data_acquisition_connection_status_changes_total`** - Total connection status changes
- **`data_acquisition_connection_duration_seconds`** - Connection duration (seconds)

## System Extensibility

The system adopts interface abstraction design, supporting flexible extension:

### Adding New PLC Protocols

1. Implement the `IPlcClientService` interface
2. Register the new protocol type in `PlcClientFactory`
3. Use the new protocol type in device configuration

### Adding New Storage Backends

1. Implement the `IDataStorageService` interface
2. Register the new storage service in `Program.cs`
3. The system will use multiple storage backends simultaneously

For detailed extension methods, please refer to the relevant instructions in [FAQ](faq.en.md).

## Next Steps

After understanding core modules, we recommend continuing to learn:

- Read [Data Processing Flow](data-flow.en.md) to understand data flow process
