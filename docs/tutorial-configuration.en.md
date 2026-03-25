# Configuration

This document describes the configuration model, field constraints, and directory rules used by DataAcquisition.

The configuration model is designed to be:

- stable at the top level
- explicit about driver selection
- extensible through `ProtocolOptions`
- validated before runtime

## Configuration Entry Points

Default device config directory:

- [src/DataAcquisition.Edge.Agent/Configs](../src/DataAcquisition.Edge.Agent/Configs)

Application settings:

- [src/DataAcquisition.Edge.Agent/appsettings.json](../src/DataAcquisition.Edge.Agent/appsettings.json)

Offline validation:

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

JSON Schema:

- [../schemas/device-config.schema.json](../schemas/device-config.schema.json)

Example configs:

- [../examples/device-configs](../examples/device-configs)

## Device Configuration Structure

Minimal example:

```json
{
  "SchemaVersion": 1,
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Driver": "melsec-a1e",
  "Host": "127.0.0.1",
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
| `SchemaVersion` | ✅ | config schema version, currently fixed at `1` |
| `IsEnabled` | ✅ | whether the device is enabled |
| `PlcCode` | ✅ | unique device identifier, must not be duplicated across files |
| `Driver` | ✅ | stable driver name such as `melsec-a1e` or `siemens-s7` |
| `Host` | ✅ | PLC endpoint host, accepts IPs and DNS hostnames |
| `Port` | ✅ | PLC endpoint port |
| `ProtocolOptions` | Optional | driver-specific parameters |
| `HeartbeatMonitorRegister` | ✅ | heartbeat register |
| `HeartbeatPollingInterval` | ✅ | heartbeat polling interval in milliseconds |
| `Channels` | ✅ | channel list |

Rules:

- `Driver` accepts full names only
- `ProtocolOptions` is not an unrestricted bag; unsupported keys are rejected
- `PlcCode` must be unique inside the config directory

## Channel Configuration

A device can contain multiple channels. Each channel usually maps to one measurement.

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
| `Measurement` | ✅ | target measurement name |
| `ChannelCode` | ✅ | channel identifier |
| `EnableBatchRead` | ✅ | whether batch read is enabled |
| `BatchReadRegister` | Conditional | batch read starting register |
| `BatchReadLength` | Conditional | batch read length |
| `BatchSize` | ✅ | queue aggregation size |
| `AcquisitionInterval` | ✅ | collection interval in milliseconds |
| `AcquisitionMode` | ✅ | `Always` or `Conditional` |
| `ConditionalAcquisition` | Conditional | trigger configuration |
| `Metrics` | Conditional | metric list |

## Metric Configuration

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
| `MetricLabel` | ✅ | human-readable label |
| `FieldName` | ✅ | stored field name |
| `Register` | ✅ | PLC address |
| `Index` | ✅ | offset inside a batch-read buffer |
| `DataType` | ✅ | data type |
| `EvalExpression` | Optional | transform expression |
| `StringByteLength` | Conditional | string byte length |
| `Encoding` | Conditional | string encoding, prefer `utf-8` |

Notes:

- fixed-length strings are sanitized to remove trailing `\0`
- expressions apply only to numeric values

## Acquisition Modes

### Always

Use for continuous signals:

```json
{
  "AcquisitionMode": "Always",
  "AcquisitionInterval": 100
}
```

### Conditional

Use for cycle boundaries and event-driven capture:

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

- formal business events are written as `Start` / `End`
- recovery diagnostics are written to `<measurement>_diagnostic`
- formal analytics should be based only on paired `Start` / `End`

## `ProtocolOptions`

`ProtocolOptions` is the driver-specific extension area.

Common keys:

- `connect-timeout-ms`
- `receive-timeout-ms`

Some drivers add their own keys, for example:

- `siemens-s7` uses `plc`
- `inovance-tcp` uses `series` and `station`
- `lsis-fast-enet` uses `cpu-type` and `slot-no`

Full details:

- [hsl-drivers.en.md](hsl-drivers.en.md)

## Configuration Directory

The default device config directory comes from app settings:

```json
{
  "Acquisition": {
    "DeviceConfigService": {
      "ConfigDirectory": "Configs"
    }
  }
}
```

Rules:

- relative paths are resolved from the application base directory
- offline validation uses the same directory by default
- `--config-dir` can override it temporarily

## Configuration Guidance

- use stable, searchable `PlcCode` and `ChannelCode` values
- prefer batch reads for contiguous registers
- do basic unit conversion during acquisition, not downstream
- validate configs before deployment
- do not push private unsupported driver parameters into `ProtocolOptions`

## Related Docs

- [Getting Started](tutorial-getting-started.en.md)
- [Driver Catalog](hsl-drivers.en.md)
- [Deployment](tutorial-deployment.en.md)
