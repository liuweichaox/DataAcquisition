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
          "MetricName": "temperature",
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

### Key Fields

- `PlcCode`: device unique ID
- `Type`: Mitsubishi / Inovance / BeckhoffAds
- `HeartbeatMonitorRegister`: heartbeat register
- `Channels`: define acquisition pipelines

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
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token",
    "Org": "your-org",
    "Bucket": "plc_data"
  },
  "Parquet": {
    "Directory": "./Data/parquet"
  },
  "Edge": {
    "EnableCentralReporting": true,
    "CentralApiBaseUrl": "http://localhost:8000",
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
