# Configuration

This document covers three layers of configuration:

- device-level configuration: how to connect to a PLC
- channel-level configuration: how to collect and shape data
- app-level configuration: how to configure primary storage, WAL, and runtime behavior

## Config Locations

- device configs: `src/DataAcquisition.Edge.Agent/Configs/*.json`
- app settings: `src/DataAcquisition.Edge.Agent/appsettings.json`

## 1. Device-Level Configuration

Minimal structure:

```json
{
  "SchemaVersion": 1,
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Driver": "melsec-a1e",
  "Host": "192.168.1.100",
  "Port": 502,
  "ProtocolOptions": {
    "connect-timeout-ms": "5000",
    "receive-timeout-ms": "5000"
  },
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": []
}
```

Field reference:

| Field | Required | Description |
|------|:--------:|-------------|
| `SchemaVersion` | ✅ | configuration structure version, currently fixed at `1` |
| `IsEnabled` | ✅ | whether the device is enabled |
| `PlcCode` | ✅ | unique device identifier |
| `Driver` | ✅ | stable driver name such as `melsec-a1e`, `melsec-mc`, `siemens-s7` |
| `Host` | ✅ | PLC endpoint host, accepts IPs and DNS hostnames |
| `Port` | ✅ | PLC endpoint port |
| `ProtocolOptions` | Optional | additional driver-specific parameters |
| `HeartbeatMonitorRegister` | ✅ | heartbeat register |
| `HeartbeatPollingInterval` | ✅ | heartbeat polling interval in milliseconds |
| `Channels` | ✅ | configured channels |

Notes:

- driver selection accepts full `Driver` names only
- `ProtocolOptions` is not an unbounded bag; unsupported keys are rejected at runtime
- documented camelCase forms such as `cpuType` and `slotNo` are also accepted
- see [hsl-drivers.en.md](hsl-drivers.en.md) for the current driver catalog
- JSON Schema: [../schemas/device-config.schema.json](../schemas/device-config.schema.json)
- Example configs: [../examples/device-configs](../examples/device-configs)

Siemens example:

```json
{
  "Driver": "siemens-s7",
  "Host": "192.168.1.20",
  "Port": 102,
  "ProtocolOptions": {
    "plc": "S1200"
  }
}
```

Inovance example:

```json
{
  "Driver": "inovance-tcp",
  "Host": "192.168.1.30",
  "Port": 502,
  "ProtocolOptions": {
    "series": "AM",
    "station": "1"
  }
}
```

## 2. Channel-Level Configuration

A device can contain multiple channels. Each channel usually maps to one business data stream or one measurement.

Example:

```json
{
  "Measurement": "sensor",
  "ChannelCode": "PLC01C01",
  "EnableBatchRead": true,
  "BatchReadRegister": "D6000",
  "BatchReadLength": 10,
  "BatchSize": 10,
  "AcquisitionInterval": 100,
  "AcquisitionMode": "Always",
  "Metrics": []
}
```

Field reference:

| Field | Required | Description |
|------|:--------:|-------------|
| `Measurement` | ✅ | primary measurement name |
| `ChannelCode` | ✅ | unique channel identifier |
| `EnableBatchRead` | ✅ | whether batch read is enabled |
| `BatchReadRegister` | Conditional | batch read starting register |
| `BatchReadLength` | Conditional | batch read length |
| `BatchSize` | ✅ | queue aggregation size before flush |
| `AcquisitionInterval` | ✅ | collection interval, `0` means no intentional delay |
| `AcquisitionMode` | ✅ | `Always` or `Conditional` |
| `ConditionalAcquisition` | Conditional | conditional acquisition configuration |
| `Metrics` | Conditional | metric definitions |

## 3. Metric Configuration

Example:

```json
{
  "MetricLabel": "temperature",
  "FieldName": "temperature",
  "Register": "D6000",
  "Index": 0,
  "DataType": "short",
  "EvalExpression": "value / 100.0"
}
```

Field reference:

| Field | Required | Description |
|------|:--------:|-------------|
| `MetricLabel` | ✅ | display label |
| `FieldName` | ✅ | stored field name |
| `Register` | ✅ | PLC register |
| `Index` | ✅ | offset inside the batch-read buffer |
| `DataType` | ✅ | supported scalar type |
| `EvalExpression` | Optional | numeric transform expression |
| `StringByteLength` | Conditional | string byte length |
| `Encoding` | Conditional | string encoding, prefer `utf-8` |

Notes:

- fixed-length string values are sanitized to remove trailing `\0`
- expressions are applied only to numeric values

## 4. Acquisition Modes

### Always

Good for continuous signals:

```json
{
  "AcquisitionMode": "Always",
  "AcquisitionInterval": 100
}
```

### Conditional

Good for cycles, state transitions, and event-driven capture:

```json
{
  "AcquisitionMode": "Conditional",
  "ConditionalAcquisition": {
    "Register": "D6006",
    "DataType": "short",
    "StartTriggerMode": "RisingEdge",
    "EndTriggerMode": "FallingEdge"
  }
}
```

Conditional semantics:

- formal cycle events are written as `Start` / `End`
- recovery diagnostics are written to `<measurement>_diagnostic`
- formal analytics should use only paired `Start` / `End`
- timestamps are stored in UTC

## 5. Batch Read

Prefer batch read when registers are contiguous:

```json
{
  "EnableBatchRead": true,
  "BatchReadRegister": "D6000",
  "BatchReadLength": 10
}
```

This reduces network round trips and single-read overhead.

## 6. App-Level Configuration

Core example:

```json
{
  "Urls": "http://+:8001",
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token",
    "Org": "default",
    "Bucket": "iot"
  },
  "Parquet": {
    "Directory": "./Data/parquet"
  },
  "Acquisition": {
    "DeviceConfigService": {
      "ConfigDirectory": "Configs"
    },
    "StateStore": {
      "DatabasePath": "Data/acquisition-state.db"
    }
  },
  "Edge": {
    "EnableCentralReporting": true,
    "CentralApiBaseUrl": "http://localhost:8000",
    "EdgeId": "EDGE-001",
    "HeartbeatIntervalSeconds": 10
  }
}
```

Important points:

- `Parquet:Directory` is the WAL root and contains `pending/`, `retry/`, and `invalid/`
- `Acquisition:DeviceConfigService:ConfigDirectory` controls the device config directory and is also used by offline validation by default
- `Acquisition:StateStore:DatabasePath` stores active cycle recovery state
- if you only want to validate the edge acquisition path first, disable `EnableCentralReporting`

## 7. Best Practices

- use stable and readable `PlcCode` and `ChannelCode`
- prefer batch read when registers are contiguous
- tune `BatchSize` based on throughput vs latency
- convert engineering units during acquisition rather than pushing raw dirty values downstream
- for conditional acquisition, define the real business start/end edges first
- validate configs before deployment with `dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs`
- use `dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs --config-dir <directory>` when validating a non-default directory

## Next

- [Deployment Tutorial](tutorial-deployment.en.md)
- [Driver Catalog](hsl-drivers.en.md)
- [Design](design.en.md)
