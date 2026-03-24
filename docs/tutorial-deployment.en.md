# Deployment

This document explains how to run DataAcquisition as a long-running, recoverable, observable PLC data collection system.

The deployment model is intentionally simple:

- `Edge Agent` is the required runtime
- `InfluxDB` is the default primary store
- `Parquet WAL` must stay on the local edge node
- `Central API / Central Web` are optional control-plane components

## Recommended Topologies

### Single Node

Suitable for local validation, labs, or one production line:

- 1 `Edge Agent`
- 1 `InfluxDB`
- optional `Central API / Central Web`

### Multi Node

Suitable for multiple workshops, lines, or factories:

- one `Edge Agent` per collection node
- local `WAL` and local state store on every edge node
- one shared `Central API / Central Web`
- centralized or site-specific `InfluxDB`

The core rule is:

- the edge node must be in the PLC-reachable network
- WAL must not depend on the central service or a remote shared directory

## Runtime Components

### Edge Agent

Responsibilities:

- load device configuration
- connect to PLCs
- run Always / Conditional acquisition
- write WAL first, then write primary storage
- expose local health, metrics, logs, and diagnostics

### InfluxDB

Responsibilities:

- act as the default primary time-series store

### Central API / Central Web

Responsibilities:

- show node status
- show heartbeats
- aggregate metrics
- proxy edge diagnostics

Important:

- the central plane is optional
- the collection path must continue even when Central is unavailable

## Recommended Release Model

Use published binaries in production. Do not treat `dotnet run` as the primary production model.

### Publish Edge Agent

```bash
dotnet publish src/DataAcquisition.Edge.Agent -c Release -o ./publish/edge
```

Start it:

```bash
./publish/edge/DataAcquisition.Edge.Agent
```

### Publish Central API

```bash
dotnet publish src/DataAcquisition.Central.Api -c Release -o ./publish/central-api
```

Start it:

```bash
./publish/central-api/DataAcquisition.Central.Api
```

### Build Central Web

```bash
cd src/DataAcquisition.Central.Web
pnpm install
pnpm run build
```

Serve the generated `dist/` folder using nginx or another static host.

## Containerization Boundary

The repository-level Compose files are mainly intended for:

- `InfluxDB`
- `Central API`
- `Central Web`

Do not present `Edge Agent` containerization as the default path.

The reason is practical:

- Edge must reach the real PLC network reliably
- field deployments often involve real NICs, VLANs, routes, and firewalls
- a host process is easier to troubleshoot than a containerized edge runtime

The recommended rule is:

- central components can be containerized
- `InfluxDB` can be containerized
- `Edge Agent` should usually run as a host process

## Runtime Data Directories

The important deployment artifact is not the binary folder. It is the runtime data directory.

The default locations to watch are:

- `Data/parquet/pending`
- `Data/parquet/retry`
- `Data/parquet/invalid`
- `Data/logs.db`
- `Data/acquisition-state.db`

Meaning:

- `pending/`: WAL files just written by the real-time path and not yet finalized against primary storage
- `retry/`: WAL files waiting for replay after primary storage failure
- `invalid/`: poison-message quarantine
- `logs.db`: local log database
- `acquisition-state.db`: active cycle recovery state

If you only watch `pending/`, you will miss the real failure signal. The long-term indicators are:

- whether `retry/` keeps growing
- whether `invalid/` starts receiving files

## Pre-Production Configuration Checklist

At minimum, verify these settings.

### Application Level

- `Urls`
- `InfluxDB:*`
- `Parquet:Directory`
- `Acquisition:DeviceConfigService:ConfigDirectory`
- `Acquisition:StateStore:DatabasePath`
- `Edge:EnableCentralReporting`
- `Edge:CentralApiBaseUrl`

### Device Level

- `PlcCode`
- `Driver`
- `Host`
- `Port`
- `ProtocolOptions`
- `Channels`

Before you go live, run:

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

That is part of the normal deployment path, not an optional trick.

## Post-Deployment Checks

After the system starts, verify these first.

### 1. Process State

- `Edge Agent` is running
- `InfluxDB` is reachable

### 2. Health Endpoint

```bash
curl http://localhost:8001/health
```

### 3. Metrics Endpoint

```bash
curl http://localhost:8001/metrics
```

### 4. WAL State

Watch:

- whether `retry/` keeps growing
- whether `invalid/` receives files

### 5. Primary Storage

Confirm that measurements are being written into InfluxDB.

## Backup Strategy

Back up at least two categories of data.

### Runtime Data

- `Data/parquet/`
- `Data/logs.db`
- `Data/acquisition-state.db`

### Primary Storage

- InfluxDB bucket data

If strong recovery matters, do not place WAL on an unreliable shared temp path.

## Operational Advice

- run `Edge Agent` under `systemd`, Windows Service, or another service manager
- keep WAL on reliable local disk
- treat Central and Edge as separate operational surfaces
- first make `Edge -> WAL -> InfluxDB` healthy, then add the central plane

## Next

- [Getting Started](tutorial-getting-started.en.md)
- [Configuration](tutorial-configuration.en.md)
- [Driver Catalog](hsl-drivers.en.md)
