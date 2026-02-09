# Getting Started Tutorial: From Zero to Running

This guide walks you through environment setup, simulator, configuration, and verification.

---

## 1. Prerequisites

### Required Software

- .NET SDK 10.0+
- InfluxDB 2.x (local installation or Docker deployment, see options below)
- Node.js 18+ (only needed for Central Web)

### Default Ports

| Service | Default Port | Description |
|---------|-------------|-------------|
| Edge Agent | `8001` | Edge collection agent (for Central API callback to query logs/metrics) |
| Central API | `8000` | Central API service |
| Central Web | `3000` | Frontend Web UI (dev mode) |
| InfluxDB | `8086` | Time-series database |

### InfluxDB Installation Options

#### Option A: Local Installation (Full Features)

Download and install from [InfluxDB Official](https://www.influxdata.com/downloads/).

#### Option B: Docker Deployment (Recommended for Quick Testing)

```bash
docker-compose -f docker-compose.tsdb.yml up -d influxdb
```

Detailed guide: [Docker InfluxDB Deployment Guide](docker-influxdb.en.md)

---

## 2. Get the Code

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
```

---

## 3. Configure InfluxDB

Create in InfluxDB:

- Organization: `default`
- Bucket: `iot`
- Token: generate one

Update Edge Agent config:

File: `src/DataAcquisition.Edge.Agent/appsettings.json`

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

> **Note**: `Urls` uses `http://+:8001` to listen on all network interfaces. Edge Agent will auto-detect the local IP at startup and report it to Central API, ensuring central proxy callbacks are reachable.

---

## 4. Start PLC Simulator

The simulator replaces a real PLC and generates continuously changing data.

```bash
cd src/DataAcquisition.Simulator
dotnet run
```

The simulator prints register data continuously.

---

## 5. Create Device Config

Create `TEST_PLC.json` in `src/DataAcquisition.Edge.Agent/Configs/`:

```json
{
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Host": "127.0.0.1",
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

---

## 6. Run Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

---

## 7. Run Central API

```bash
dotnet run --project src/DataAcquisition.Central.Api
```

Verify:

- Health: `http://localhost:8000/health`
- Metrics: `http://localhost:8000/metrics`

---

## 8. Run Central Web

```bash
cd src/DataAcquisition.Central.Web
pnpm install
pnpm run serve
```

Open `http://localhost:3000`.

---

## 9. Verify Data

Use Flux query:

```flux
from(bucket: "iot")
  |> range(start: -10m)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> yield(name: "latest")
```

---

## 10. Verify WAL

Stop InfluxDB and observe:

- `src/DataAcquisition.Edge.Agent/Data/parquet/pending/` has WAL files
- After DB recovery, files are retried and cleaned

---

## Next Steps

- [Configuration Tutorial](tutorial-configuration.en.md)
- [Data Query Tutorial](tutorial-data-query.en.md)
- [Deployment Tutorial](tutorial-deployment.en.md)
- [Development Tutorial](tutorial-development.en.md)
