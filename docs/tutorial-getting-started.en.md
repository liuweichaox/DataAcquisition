# Getting Started

This guide follows the shortest working path: start InfluxDB, generate PLC data with the simulator, run the Edge Agent, and verify both primary storage and WAL behavior.

## Prerequisites

- .NET 10 SDK
- InfluxDB 2.x
- Node.js 20+ only if you want to run Central Web

Default ports:

| Service | Port |
|---------|------|
| Edge Agent | `8001` |
| Central API | `8000` |
| Central Web | `3000` |
| InfluxDB | `8086` |

## 1. Clone the Repository

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
```

## 2. Start InfluxDB

The fastest path is the Compose file already included in the repository:

```bash
docker compose -f docker-compose.tsdb.yml up -d
```

See [docker-influxdb.en.md](docker-influxdb.en.md) for more detail.

## 3. Configure the Edge Agent

Edit [src/DataAcquisition.Edge.Agent/appsettings.json](../src/DataAcquisition.Edge.Agent/appsettings.json):

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
    "StateStore": {
      "DatabasePath": "Data/acquisition-state.db"
    }
  },
  "Edge": {
    "EnableCentralReporting": false,
    "CentralApiBaseUrl": "http://localhost:8000",
    "EdgeId": "EDGE-001",
    "HeartbeatIntervalSeconds": 10
  }
}
```

If you are only validating the acquisition path, keep `EnableCentralReporting` set to `false` first so central registration noise does not distract from edge-side troubleshooting.

## 4. Start the PLC Simulator

```bash
dotnet run --project src/DataAcquisition.Simulator
```

The simulator prints changing register values and can be used instead of a real PLC during development.

## 5. Prepare a Device Config

Use [src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json](../src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json) as reference. Minimal example:

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

Driver selection accepts full `Driver` names only. See [hsl-drivers.en.md](hsl-drivers.en.md) for the catalog.
If your editor supports JSON Schema, point it to [../schemas/device-config.schema.json](../schemas/device-config.schema.json).

## 6. Run the Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

If the device config and InfluxDB are correct, the console should show acquisition and storage activity.

Validate configs without starting the runtime:

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

## 7. Verify Primary Storage

Run this Flux query in InfluxDB:

```flux
from(bucket: "iot")
  |> range(start: -10m)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> yield(name: "latest")
```

If rows are returned, the main acquisition path is working.

## 8. Verify WAL Behavior

Stop InfluxDB while keeping the Edge Agent running. Then observe:

- new files may appear briefly under `pending/`
- if the primary store keeps failing, files are moved into `retry/`
- poison messages that cannot be written to WAL are quarantined into `invalid/`

Default paths:

- `src/DataAcquisition.Edge.Agent/Data/parquet/pending/`
- `src/DataAcquisition.Edge.Agent/Data/parquet/retry/`
- `src/DataAcquisition.Edge.Agent/Data/parquet/invalid/`

## 9. Optional: Start the Central Side

The central side is not part of the main acquisition path. It is usually better to validate Edge first.

Run Central API:

```bash
dotnet run --project src/DataAcquisition.Central.Api
```

Run Central Web:

```bash
cd src/DataAcquisition.Central.Web
pnpm install
pnpm run serve
```

## Next

- [Configuration Tutorial](tutorial-configuration.en.md)
- [Deployment Tutorial](tutorial-deployment.en.md)
- [Design](design.en.md)
- [Development Tutorial](tutorial-development.en.md)
