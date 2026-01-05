# ⚙️ Configuration Guide

This document provides detailed configuration instructions for the DataAcquisition system.

## Related Documents

- [Getting Started Guide](getting-started.en.md) - Get started from scratch

## Device Configuration Files

Device configuration files are located in the `src/DataAcquisition.Edge.Agent/Configs/` directory, with one JSON configuration file per PLC device.

### Device Configuration File Example

The following is an actual configuration example (based on `TEST_PLC.json` in the project):

```json
{
  "IsEnabled": true,
  "PlcCode": "TEST_PLC",
  "Host": "127.0.0.1",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 2000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "CH01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 14,
      "BatchSize": 10,
      "AcquisitionInterval": 0,
      "AcquisitionMode": "Always",
      "Metrics": [
        {
          "MetricName": "temperature",
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        },
        {
          "MetricName": "pressure",
          "FieldName": "pressure",
          "Register": "D6001",
          "Index": 2,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        }
      ]
    },
    {
      "Measurement": "production",
      "ChannelCode": "CH01",
      "EnableBatchRead": false,
      "BatchReadRegister": null,
      "BatchReadLength": 0,
      "BatchSize": 1,
      "AcquisitionInterval": 0,
      "AcquisitionMode": "Conditional",
      "Metrics": null,
      "ConditionalAcquisition": {
        "Register": "D6006",
        "DataType": "short",
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

**Configuration Notes:**
- The first channel uses `Always` mode for continuous sensor data acquisition
- The second channel uses `Conditional` mode, triggered by production serial number changes
- `AcquisitionInterval` of 0 means highest frequency acquisition (no delay)
- `Metrics` can be `null` in conditional acquisition mode

### Device Configuration Properties

#### Root Level Properties

| Property Name                  | Type      | Required | Description                                      |
| ------------------------------ | --------- | -------- | ------------------------------------------------ |
| `IsEnabled`                    | `boolean` | Yes      | Whether the device is enabled                    |
| `PlcCode`                      | `string`  | Yes      | Unique identifier for the PLC device             |
| `Host`                         | `string`  | Yes      | IP address of the PLC device                     |
| `Port`                         | `integer` | Yes      | Communication port of the PLC device             |
| `Type`                         | `string`  | Yes      | PLC device type (Mitsubishi, Inovance, BeckhoffAds)|
| `HeartbeatMonitorRegister`     | `string`  | No       | Register address for monitoring PLC heartbeat    |
| `HeartbeatPollingInterval`     | `integer` | No       | Polling interval for heartbeat monitoring (ms)   |
| `Channels`                     | `array`   | Yes      | List of data acquisition channel configurations   |

#### Channels Array Properties

| Property Name                  | Type      | Required | Description                                                                         |
| ------------------------------ | --------- | -------- | ----------------------------------------------------------------------------------- |
| `Measurement`                  | `string`  | Yes      | Measurement name in the time-series database (table name)                          |
| `ChannelCode`                  | `string`  | Yes      | Unique identifier for the acquisition channel                                      |
| `BatchSize`                    | `integer` | No       | Number of data points for batch writes to the database                             |
| `AcquisitionInterval`          | `integer` | Yes      | Time interval for data acquisition (milliseconds), 0 means highest frequency (no delay) |
| `AcquisitionMode`              | `string`  | Yes      | Acquisition mode (Always: continuous acquisition, Conditional: conditional trigger acquisition) |
| `EnableBatchRead`              | `boolean` | No       | Whether to enable batch read functionality                                         |
| `BatchReadRegister`            | `string`  | No       | Starting register address for batch reads                                          |
| `BatchReadLength`              | `integer` | No       | Number of registers to read in batch                                               |
| `Metrics`                      | `array`   | No       | List of metric configurations (can be null in conditional acquisition mode)      |
| `ConditionalAcquisition`       | `object`  | No       | Conditional acquisition configuration (required only when AcquisitionMode is Conditional) |

#### Metrics Array Properties

| Property Name      | Type      | Required | Description                                                          |
| ------------------ | --------- | -------- | -------------------------------------------------------------------- |
| `MetricName`       | `string`  | Yes      | Metric name, used to identify the metric                             |
| `FieldName`        | `string`  | Yes      | Field name in the time-series database                               |
| `Register`         | `string`  | Yes      | PLC register address corresponding to the metric                     |
| `Index`            | `integer` | No       | Index position in the result when using batch reads                  |
| `DataType`         | `string`  | Yes      | Data type (e.g., short, int, float, etc.)                            |
| `EvalExpression`   | `string`  | No       | Data transformation expression (use value variable to represent raw value) |

#### ConditionalAcquisition Object Properties

| Property Name       | Type     | Required | Description                                                                          |
| ------------------- | -------- | -------- | ------------------------------------------------------------------------------------ |
| `Register`          | `string` | Yes      | Register address monitored for conditional triggering                                |
| `DataType`          | `string` | Yes      | Data type of the conditional trigger register                                        |
| `StartTriggerMode`  | `string` | Yes      | Trigger mode for starting acquisition (RisingEdge: trigger on value increase, FallingEdge: trigger on value decrease) |
| `EndTriggerMode`    | `string` | Yes      | Trigger mode for ending acquisition (RisingEdge: trigger on value increase, FallingEdge: trigger on value decrease) |

### AcquisitionTrigger Trigger Mode Explanation

| Trigger Mode   | Description                                                      |
| -------------- | ---------------------------------------------------------------- |
| `RisingEdge`   | Trigger when value changes from smaller to larger (prev < curr)  |
| `FallingEdge`  | Trigger when value changes from larger to smaller (prev > curr)  |

> Note: The RisingEdge and FallingEdge here are different from traditional edge triggering (0→1 or 1→0). They are based on value increase/decrease changes, not strict 0/1 transitions.

## Edge Agent Application Configuration (appsettings.json)

Complete configuration example for Edge Agent is located at `src/DataAcquisition.Edge.Agent/appsettings.json`:

```json
{
  "Urls": "http://localhost:8001",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "DatabasePath": "Data/logs.db"
  },
  "AllowedHosts": "*",
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token-here",
    "Bucket": "plc_data",
    "Org": "your-org"
  },
  "Parquet": {
    "Directory": "./Data/parquet"
  },
  "Edge": {
    "EnableCentralReporting": true,
    "CentralApiBaseUrl": "http://localhost:8000",
    "EdgeId": "EDGE-001",
    "HeartbeatIntervalSeconds": 10
  },
  "Acquisition": {
    "ChannelCollector": {
      "ConnectionCheckRetryDelayMs": 100,
      "TriggerWaitDelayMs": 100
    },
    "QueueService": {
      "FlushIntervalSeconds": 5,
      "RetryIntervalSeconds": 10,
      "MaxRetryCount": 3
    },
    "DeviceConfigService": {
      "ConfigChangeDetectionDelayMs": 500
    }
  }
}
```

### Edge Agent Configuration Properties

| Configuration Path                                           | Type      | Required | Default Value              | Description                                                                             |
| ------------------------------------------------------------ | --------- | -------- | -------------------------- | --------------------------------------------------------------------------------------- |
| `Urls`                                                       | `string`  | No       | `http://localhost:8001`    | Edge Agent service listening address, supports multiple addresses (separated by `;` or `,`) |
| `Logging:DatabasePath`                                       | `string`  | No       | `Data/logs.db`             | SQLite log database file path (relative path is relative to application directory)      |
| `InfluxDB:Url`                                               | `string`  | Yes      | -                          | InfluxDB server address                                                                 |
| `InfluxDB:Token`                                             | `string`  | Yes      | -                          | InfluxDB authentication token                                                           |
| `InfluxDB:Bucket`                                            | `string`  | Yes      | -                          | InfluxDB bucket name                                                                    |
| `InfluxDB:Org`                                               | `string`  | Yes      | -                          | InfluxDB organization name                                                              |
| `Parquet:Directory`                                          | `string`  | No       | `./Data/parquet`           | Parquet WAL file storage directory (relative path is relative to application directory). The system will create two subfolders under this directory: `pending` (newly created WAL files) and `retry` (WAL files that need retry) |
| `Edge:EnableCentralReporting`                                | `boolean` | No       | `true`                     | Whether to enable registration and heartbeat reporting to Central API                   |
| `Edge:CentralApiBaseUrl`                                     | `string`  | No       | `http://localhost:8000`    | Central API service address                                                             |
| `Edge:EdgeId`                                                | `string`  | No       | Auto-generated             | Edge node unique identifier, auto-generated and persisted to local file if empty       |
| `Edge:HeartbeatIntervalSeconds`                              | `integer` | No       | `10`                       | Interval for sending heartbeat to Central API (seconds)                                 |
| `Acquisition:ChannelCollector:ConnectionCheckRetryDelayMs`   | `integer` | No       | `100`                      | PLC connection check retry delay (milliseconds)                                         |
| `Acquisition:ChannelCollector:TriggerWaitDelayMs`            | `integer` | No       | `100`                      | Conditional trigger wait delay (milliseconds)                                           |
| `Acquisition:QueueService:FlushIntervalSeconds`              | `integer` | No       | `5`                        | Queue batch flush interval (seconds)                                                    |
| `Acquisition:QueueService:RetryIntervalSeconds`              | `integer` | No       | `10`                       | Retry interval (seconds)                                                                |
| `Acquisition:QueueService:MaxRetryCount`                     | `integer` | No       | `3`                        | Maximum retry count                                                                     |
| `Acquisition:DeviceConfigService:ConfigChangeDetectionDelayMs` | `integer` | No       | `500`                      | Device configuration file change detection delay (milliseconds)                         |

> **Tips**:
> - Device configuration files (PLC configurations) are stored in the `Configs/` directory, format is `*.json`
> - All path configurations support relative and absolute paths, relative paths are relative to the application's working directory
> - Configuration can be overridden via environment variables, e.g., `ASPNETCORE_URLS` can override the `Urls` configuration

## 📊 Configuration to Database Mapping

The system maps configuration files to InfluxDB time-series database. The following is the mapping relationship:

### Mapping Relationship Table

| Configuration File Field                     | InfluxDB Structure      | Description                                    | Example Value                    |
| -------------------------------------------- | ----------------------- | ---------------------------------------------- | -------------------------------- |
| `Channels[].Measurement`                     | **Measurement**         | Measurement name in time-series database (table name) | `"sensor"`                       |
| `PlcCode`                                    | **Tag**: `plc_code`     | PLC device code tag                            | `"M01C123"`                      |
| `Channels[].ChannelCode`                     | **Tag**: `channel_code` | Channel code tag                               | `"M01C01"`                       |
| `EventType`                                  | **Tag**: `event_type`   | Event type tag (Start/End/Data)                | `"Start"`, `"End"`, `"Data"`     |
| `Channels[].Metrics[].FieldName`            | **Field**               | Data field name                                | `"up_temp"`, `"down_temp"`       |
| `CycleId`                                    | **Field**: `cycle_id`   | Acquisition cycle unique identifier (GUID)     | `"guid-xxx"`                     |
| Acquisition time                             | **Timestamp**           | Data point timestamp (local time)              | `2025-01-15T10:30:00`           |

### Configuration Example and Line Protocol

**Configuration File** (`M01C123.json`):

```json
{
  "IsEnabled": true,
  "PlcCode": "M01C123",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "M01C01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 10,
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Conditional",
      "Metrics": [
        {
          "MetricName": "up_temp",
          "FieldName": "up_temp",
          "Register": "D6002",
          "Index": 2,
          "DataType": "short"
        },
        {
          "MetricName": "down_temp",
          "FieldName": "down_temp",
          "Register": "D6004",
          "Index": 4,
          "DataType": "short",
          "EvalExpression": "value / 1000.0"
        }
      ],
      "ConditionalAcquisition": {
        "Register": "D6006",
        "DataType": "short",
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

**Generated InfluxDB Line Protocol**:

**Start Event** (conditional acquisition start):

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Start up_temp=250i,down_temp=0.18,cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312200000000000
```

**Data Event** (normal data point):

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Data up_temp=255i,down_temp=0.19 1705312210000000000
```

**End Event** (conditional acquisition end):

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=End cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312300000000000
```

### Line Protocol Format Explanation

InfluxDB Line Protocol format:

```
measurement,tag1=value1,tag2=value2 field1=value1,field2=value2 timestamp
```

**Field Type Explanation**:

- **Measurement**: From configuration's `Measurement`, e.g., `"sensor"`
- **Tags** (for filtering and grouping, indexed fields):
  - `plc_code`: PLC device code
  - `channel_code`: Channel code
  - `event_type`: Event type (`Start`/`End`/`Data`)
- **Fields** (actual data values):
  - All fields from `Metrics[].FieldName` (e.g., `up_temp`, `down_temp`)
  - `cycle_id`: Conditional acquisition cycle ID (GUID, used to associate Start/End events)
  - Numeric types: integers use `i` suffix (e.g., `250i`), floating-point numbers are written directly (e.g., `0.18`)
- **Timestamp**: Data acquisition time (local time, nanosecond precision)

### Query Examples

**Query data for a specific PLC's acquisition channel within a specified time range (1h)**:

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["plc_code"] == "M01C123")
  |> filter(fn: (r) => r["channel_code"] == "M01C01")
```

**Query complete cycle of conditional acquisition**:

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["cycle_id"] == "550e8400-e29b-41d4-a716-446655440000")
```

## Next Steps

After configuration, we recommend continuing to learn:

- Read [API Usage Examples](api-usage.en.md) to learn how to query data and manage the system via API
