# Edge Agent Application Configuration

## Configuration File Location

Complete Edge Agent configuration example is located at `src/DataAcquisition.Edge.Agent/appsettings.json`.

## Configuration Example

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

## Configuration Properties

### Basic Configuration

| Configuration Path | Type | Required | Default | Description |
|-------------------|------|----------|---------|-------------|
| `Urls` | `string` | No | `http://localhost:8001` | Edge Agent service listening address, supports multiple addresses (separated by `;` or `,`) |

### Logging Configuration

| Configuration Path | Type | Required | Default | Description |
|-------------------|------|----------|---------|-------------|
| `Logging:DatabasePath` | `string` | No | `Data/logs.db` | SQLite log database file path (relative path is relative to application directory) |

### InfluxDB Configuration

| Configuration Path | Type | Required | Default | Description |
|-------------------|------|----------|---------|-------------|
| `InfluxDB:Url` | `string` | Yes | - | InfluxDB server address |
| `InfluxDB:Token` | `string` | Yes | - | InfluxDB authentication token |
| `InfluxDB:Bucket` | `string` | Yes | - | InfluxDB bucket name |
| `InfluxDB:Org` | `string` | Yes | - | InfluxDB organization name |

### Parquet Configuration

| Configuration Path | Type | Required | Default | Description |
|-------------------|------|----------|---------|-------------|
| `Parquet:Directory` | `string` | No | `./Data/parquet` | Parquet WAL file storage directory (relative path is relative to application directory) |

### Edge Node Configuration

| Configuration Path | Type | Required | Default | Description |
|-------------------|------|----------|---------|-------------|
| `Edge:EnableCentralReporting` | `boolean` | No | `true` | Whether to enable registration and heartbeat reporting to Central API |
| `Edge:CentralApiBaseUrl` | `string` | No | `http://localhost:8000` | Central API service address |
| `Edge:EdgeId` | `string` | No | Auto-generated | Edge node unique identifier, auto-generated and persisted to local file if empty |
| `Edge:HeartbeatIntervalSeconds` | `integer` | No | `10` | Heartbeat interval sent to Central API (seconds) |

### Acquisition Service Configuration

| Configuration Path | Type | Required | Default | Description |
|-------------------|------|----------|---------|-------------|
| `Acquisition:ChannelCollector:ConnectionCheckRetryDelayMs` | `integer` | No | `100` | PLC connection check retry delay (milliseconds) |
| `Acquisition:ChannelCollector:TriggerWaitDelayMs` | `integer` | No | `100` | Conditional trigger wait delay (milliseconds) |
| `Acquisition:QueueService:FlushIntervalSeconds` | `integer` | No | `5` | Queue batch flush interval (seconds) |
| `Acquisition:QueueService:RetryIntervalSeconds` | `integer` | No | `10` | Retry interval (seconds) |
| `Acquisition:QueueService:MaxRetryCount` | `integer` | No | `3` | Maximum retry count |
| `Acquisition:DeviceConfigService:ConfigChangeDetectionDelayMs` | `integer` | No | `500` | Device config file change detection delay (milliseconds) |

## Configuration Tips

- Device configuration files (PLC configs) are stored in the `Configs/` directory, format is `*.json`
- All path configurations support both relative and absolute paths, relative paths are relative to the application working directory
- Configurations can be overridden via environment variables, e.g., `ASPNETCORE_URLS` can override the `Urls` configuration

## Related Documentation

- [Device Configuration Guide](./device-config.en.md)
- [Configuration to Database Mapping](./database-mapping.en.md)
