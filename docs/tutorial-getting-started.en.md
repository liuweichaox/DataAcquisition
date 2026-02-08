# Getting Started Tutorial: From Zero to Running

This guide walks you through environment setup, simulator, configuration, and verification.

---

## 1. Prerequisites

### Required Software

- .NET SDK 10.0+
- InfluxDB 2.x (local installation or Docker deployment, see options below)
- Node.js 18+ (only needed for Central Web)

### Default Ports

- Central API: `8000`
- Central Web: `3000`
- InfluxDB: `8086`

### InfluxDB Installation Options

#### Option A: Local Installation (Full Features)

Download and install from [InfluxDB Official](https://www.influxdata.com/downloads/).

#### Option B: Docker Deployment (Recommended for Quick Testing)

```bash
docker-compose up -d influxdb
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

## 4. Start PLC Simulator

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
npm install
npm run serve
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
