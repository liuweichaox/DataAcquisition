# Getting Started

The goal of this guide is simple: run a local Edge Agent, validate device configuration, and confirm the main acquisition pipeline can start and write to primary storage.

## Prerequisites

- .NET 10 SDK
- Docker
- InfluxDB 2.x

If you only want to validate configuration first, you do not need a real PLC yet.

## Step 1: Build the Solution

From the repository root:

```bash
dotnet build DataAcquisition.sln
```

## Step 2: Start InfluxDB

The repository includes a simple compose file:

```bash
docker compose -f docker-compose.tsdb.yml up -d
```

If you already have your own InfluxDB instance, just make sure the `InfluxDB` section in [appsettings.json](../src/DataAcquisition.Edge.Agent/appsettings.json) points to the right endpoint.

## Step 3: Review Device Configuration

The default device config directory is:

- [src/DataAcquisition.Edge.Agent/Configs](../src/DataAcquisition.Edge.Agent/Configs)

The repository already includes a local development sample:

- [TEST_PLC.json](../src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json)

You can also use:

- [examples/device-configs](../examples/device-configs)
- [device-config.schema.json](../schemas/device-config.schema.json)

## Step 4: Validate Configuration Offline

Validate configs before starting the runtime:

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

To validate another directory:

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs --config-dir ./examples/device-configs
```

On success, you should see output like:

```text
[OK] .../TEST_PLC.json (TEST_PLC)
```

## Step 5: Start the Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

The default URL comes from [appsettings.json](../src/DataAcquisition.Edge.Agent/appsettings.json):

- `http://localhost:8001`

Useful endpoints after startup:

- `/health`
- `/metrics`
- `/api/logs`
- `/api/DataAcquisition/plc-connections`

## Optional: Start the PLC Simulator

For a local closed-loop workflow, start the simulator:

```bash
dotnet run --project src/DataAcquisition.Simulator
```

The simulator listens on port `502` by default and prints changing registers to the console. Details:

- [src/DataAcquisition.Simulator/README.md](../src/DataAcquisition.Simulator/README.md)

## How to Verify It Is Working

You can verify the system from four angles.

### 1. Agent liveness

```bash
curl http://localhost:8001/health
```

### 2. Config loading

Startup logs should show successful config validation and runtime startup for PLCs/channels.

### 3. WAL directories

Default WAL root:

- `src/DataAcquisition.Edge.Agent/bin/Debug/net10.0/Data/parquet`

Internal state directories:

- `pending/`
- `retry/`
- `invalid/`

Meaning:

- `pending/` is the transient state for newly written WAL files
- `retry/` contains files that failed primary storage writes
- `invalid/` contains poisoned messages that could not be written to WAL

### 4. InfluxDB writes

If InfluxDB is reachable, WAL files should be consumed quickly instead of accumulating under `retry/`.

## Optional: Start Central Components

Central services are not required for the acquisition path itself, but you can run them for registration, heartbeat, and UI:

```bash
dotnet run --project src/DataAcquisition.Central.Api
```

To run the web UI:

```bash
cd src/DataAcquisition.Central.Web
pnpm install
pnpm run serve
```

## Common Problems

### Config validation fails

Check:

- whether `Driver` is a full stable driver name
- whether `ProtocolOptions` contains keys unsupported by that driver
- whether `PlcCode` is duplicated across files

### WAL files keep moving into `retry`

This usually means primary storage is unavailable, for example an incorrect InfluxDB endpoint or a stopped service.

### The simulator works, but the real PLC does not

Check:

- `Host` and `Port`
- network connectivity
- whether the selected driver matches the real device/protocol

## Next

- [Configuration](tutorial-configuration.en.md)
- [Driver Catalog](hsl-drivers.en.md)
- [Deployment](tutorial-deployment.en.md)
