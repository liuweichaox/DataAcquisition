# Deployment

This document explains how to deploy DataAcquisition as a long-running, observable, real-time-first PLC data acquisition system.

The recommended deployment principles are:

- `Edge Agent` is the required runtime and should be deployed close to PLCs
- `InfluxDB` is the default TSDB implementation
- local runtime state is limited to logs and conditional acquisition state, without a WAL or replay directories
- `Central API / Central Web` are optional control-plane components

## Recommended Deployment Topologies

### Single Node

Suitable for local validation, labs, or one production line:

- 1 `Edge Agent`
- 1 `InfluxDB`
- optional `Central API / Central Web`

### Multi Node

Suitable for multiple workshops, lines, or factories:

- one `Edge Agent` per collection node
- local logs and conditional acquisition state on every edge node
- one shared `Central API / Central Web`
- centralized or site-specific `InfluxDB`

The core rule is:

- the edge node must be in the PLC-reachable network
- TSDB reachability matters more than control-plane reachability

## Runtime Components

### Edge Agent

Responsibilities:

- load device configuration
- connect to PLCs
- run Always / Conditional acquisition
- write batches directly into InfluxDB
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

## Runtime Files

The important deployment artifact is not only the binary folder. It is also the local runtime state.

The default files to watch are:

- `Data/logs.db`
- `Data/acquisition-state.db`

Meaning:

- `logs.db`: local log database
- `acquisition-state.db`: active-cycle recovery state for conditional acquisition

The runtime does not keep a local raw-data replay backlog.

## Pre-Production Configuration Checklist

At minimum, verify these settings.

### Application Level

- `Urls`
- `InfluxDB:*`
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

### 4. Logs and Errors

Watch for:

- PLC connectivity errors
- TSDB write failures
- configuration reload problems

### 5. Storage Writes

Confirm that measurements are being written into InfluxDB.

## Backup Strategy

If you need to retain diagnostics and conditional acquisition context, back up at least two categories of data.

### Local Runtime State

- `Data/logs.db`
- `Data/acquisition-state.db`

### Storage

- InfluxDB bucket data

Because the current runtime does not depend on a local raw-data replay queue, the primary backup target should be InfluxDB itself.

## Operational Advice

- run `Edge Agent` under `systemd`, Windows Service, or another service manager
- treat Central and Edge as separate operational surfaces
- first make `Edge -> InfluxDB` healthy, then add the central plane
- if TSDB writes fail, treat that as an operational alarm to fix immediately rather than something a replay worker will clean up later

## Related Docs

- [Getting Started](tutorial-getting-started.en.md)
- [Configuration](tutorial-configuration.en.md)
- [Driver Catalog](hsl-drivers.en.md)
