# Configuration Tutorial: Devices, Channels, and Modes

This guide explains device configs and application settings with examples and best practices.

---

## 1. Config Locations

- Device configs: `src/DataAcquisition.Edge.Agent/Configs/*.json`
- App settings: `src/DataAcquisition.Edge.Agent/appsettings.json`

---

## 2. Device Config Structure

```json
{
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "PLC01C01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 10,
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Always",
      "Metrics": [
        {
          "MetricLabel": "temperature",
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        }
      ]
    }
  ]
}
```

### Field Reference

#### Device Level (DeviceConfig)

| Field | Type | Required | Description |
|-------|------|:--------:|-------------|
| `IsEnabled` | `bool` | ✅ | Whether to enable data acquisition for this device |
| `PlcCode` | `string` | ✅ | Unique PLC identifier |
| `Host` | `string` | ✅ | PLC IP address |
| `Port` | `ushort` | ✅ | Communication port (e.g., Modbus default 502) |
| `Type` | `enum` | ✅ | PLC type: `Mitsubishi`, `Inovance`, `BeckhoffAds` |
| `HeartbeatMonitorRegister` | `string` | ✅ | Heartbeat detection register address (e.g., `D100`) |
| `HeartbeatPollingInterval` | `int` | ✅ | Heartbeat polling interval in milliseconds |
| `Channels` | `array` | ✅ | List of acquisition channels (split by business or function) |

#### Channel Level (Channel)

| Field | Type | Required | Description |
|-------|------|:--------:|-------------|
| `ChannelCode` | `string` | ✅ | Unique channel identifier |
| `Measurement` | `string` | ✅ | Time-series database table name (measurement) |
| `EnableBatchRead` | `bool` | ✅ | Enable batch reading to read a contiguous register block in one request |
| `BatchReadRegister` | `string` | Cond. | Starting register address for batch read (required when `EnableBatchRead=true`) |
| `BatchReadLength` | `ushort` | Cond. | Number of registers to read in batch (word count) |
| `BatchSize` | `int` | ✅ | Number of data points to buffer before flushing to the database |
| `AcquisitionInterval` | `int` | ✅ | Acquisition interval in milliseconds; `0` for maximum frequency (no delay) |
| `AcquisitionMode` | `enum` | ✅ | Acquisition mode: `Always` (continuous) or `Conditional` (trigger-based) |
| `ConditionalAcquisition` | `object` | Cond. | Conditional acquisition config (required for `Conditional` mode) |
| `Metrics` | `array` | Cond. | List of metrics to collect (required for `Always` mode) |

#### Conditional Acquisition (ConditionalAcquisition)

| Field | Type | Required | Description |
|-------|------|:--------:|-------------|
| `Register` | `string` | ✅ | Trigger register address |
| `DataType` | `string` | ✅ | Data type of the trigger register |
| `StartTriggerMode` | `enum` | ✅ | Start trigger: `RisingEdge` (value changes from 0 to non-zero) or `FallingEdge` (non-zero to 0) |
| `EndTriggerMode` | `enum` | ✅ | End trigger: same options as above |

#### Metric Level (Metric)

| Field | Type | Required | Description |
|-------|------|:--------:|-------------|
| `MetricLabel` | `string` | ✅ | Label to identify the metric |
| `FieldName` | `string` | ✅ | Field name in the time-series database |
| `Register` | `string` | ✅ | PLC register address (e.g., `D6000`) |
| `Index` | `int` | ✅ | Byte offset within the batch read buffer |
| `DataType` | `string` | ✅ | Data type: `short`, `ushort`, `int`, `uint`, `float`, `double`, `long`, `ulong`, `string` |
| `EvalExpression` | `string` | ❌ | Value conversion expression (e.g., `value / 100.0`); raw value used if omitted |
| `StringByteLength` | `int` | Cond. | String byte length (required when `DataType=string`) |
| `Encoding` | `string` | Cond. | String encoding (used when `DataType=string`) |

---

## 3. Acquisition Modes

### Always (continuous)

```json
{
  "AcquisitionMode": "Always",
  "AcquisitionInterval": 100
}
```

### Conditional (event-driven)

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

Conditional mode emits Start/End events with CycleId.

---

## 4. Batch Read

```json
{
  "EnableBatchRead": true,
  "BatchReadRegister": "D6000",
  "BatchReadLength": 10
}
```

- `Index` maps each field to the batch result position

---

## 5. Data Transform

```json
{
  "FieldName": "temperature",
  "Register": "D6000",
  "DataType": "short",
  "EvalExpression": "value / 100.0"
}
```

---

## 6. App Settings (appsettings.json)

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
  "Edge": {
    "EnableCentralReporting": true,
    "CentralApiBaseUrl": "http://localhost:8000",
    "EdgeId": "EDGE-001",
    "HeartbeatIntervalSeconds": 10
  }
}
```

---

## 7. Hot Reload

- Changes in `Configs/*.json` are auto-applied
- Default debounce is 500ms

---

## 8. Best Practices

- Consistent naming for PlcCode/ChannelCode
- Prefer batch reads to reduce network RTT
- Tune BatchSize to balance latency and throughput
- Convert units at acquisition time

---

## Troubleshooting

- No data: verify host/port/registers
- Conditional not triggered: validate register changes and trigger modes
- InfluxDB empty: check token/org/bucket

---

Next: [Deployment Tutorial](tutorial-deployment.en.md)
