# DataAcquisition

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](https://dotnet.microsoft.com/)

中文: [README.md](README.md)

DataAcquisition is an open-source PLC data acquisition runtime for industrial edge environments. It focuses on three things:

- reliable collection across multiple PLCs and channels
- local recoverability through a WAL-first pipeline
- clean extensibility through stable driver names and explicit provider contracts

It is intentionally closer to a deployable edge runtime than a full MES/SCADA platform.

## Scope

The current project focuses on:

- shop-floor Edge Agent collection
- InfluxDB as primary time-series storage and Parquet as local WAL
- conditional cycle tracking and recovery diagnostics
- central edge registration, heartbeat, and diagnostics proxying
- Prometheus metrics and a Web dashboard

The project does not try to:

- replace a complete SCADA or MES stack
- abstract every PLC vendor detail into one universal model
- expose every private driver parameter as a global configuration field

## Architecture

Core data path:

```text
PLC -> ChannelCollector -> QueueService -> Parquet WAL -> InfluxDB
                                   |               |
                                   |               -> retry/
                                   -> invalid/
```

Deployment topology:

```text
Central Web -> Central API -> Edge Agent -> PLC
```

Design highlights:

- WAL-first durability
- UTC time semantics across nodes
- separation between formal business events and diagnostic recovery events
- clear extension points through `IPlcDriverProvider` and `IPlcClientService`

See [docs/design.en.md](docs/design.en.md) and [docs/data-flow.en.md](docs/data-flow.en.md) for more detail.

## Core Capabilities

- async parallel collection across multiple PLCs and channels
- `Always` and `Conditional` acquisition modes
- RisingEdge / FallingEdge trigger handling
- contiguous register batch reads
- hot reload for device configuration
- WAL lifecycle with `pending/`, `retry/`, and `invalid/`
- local persistence for active cycle recovery
- central registration, heartbeat, metrics, and log proxying

## Driver Model

The driver model is designed to stay simple for operators and explicit for developers.

Default behavior:

- HslCommunication is the built-in communication implementation
- protocols are selected by stable `Driver` names
- examples: `melsec-a1e`, `melsec-mc`, `siemens-s7`

Standard configuration fields:

- `Driver`
- `Host`
- `Port`
- `ProtocolOptions`
- `Channels`

Configuration assets:

- JSON Schema: [schemas/device-config.schema.json](schemas/device-config.schema.json)
- Example configs: [examples/device-configs](examples/device-configs)
- Offline validation: `dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs`
- the default validation directory comes from `Acquisition:DeviceConfigService:ConfigDirectory` and can be overridden with `--config-dir`

Extension model:

- the runtime core depends only on [IPlcDriverProvider](src/DataAcquisition.Application/Abstractions/IPlcDriverProvider.cs)
- custom drivers can be added without changing the acquisition pipeline

Driver catalog: [docs/hsl-drivers.en.md](docs/hsl-drivers.en.md)

## Repository Layout

- [src/DataAcquisition.Edge.Agent](src/DataAcquisition.Edge.Agent)  
  edge runtime and main executable

- [src/DataAcquisition.Infrastructure](src/DataAcquisition.Infrastructure)  
  PLC drivers, acquisition orchestration, WAL, storage, logging, and metrics

- [src/DataAcquisition.Application](src/DataAcquisition.Application)  
  application-layer abstractions and runtime contracts

- [src/DataAcquisition.Domain](src/DataAcquisition.Domain)  
  domain and configuration models

- [src/DataAcquisition.Central.Api](src/DataAcquisition.Central.Api)  
  central service for edge registration, heartbeat, and diagnostics proxying

- [src/DataAcquisition.Central.Web](src/DataAcquisition.Central.Web)  
  Vue3 monitoring UI

- [tests/DataAcquisition.Core.Tests](tests/DataAcquisition.Core.Tests)  
  current core test project

## Quick Start

Prerequisites:

- .NET 10 SDK
- InfluxDB 2.x
- Node.js 20+ for Central Web only

1. Build the solution

```bash
dotnet build DataAcquisition.sln
```

2. Start InfluxDB

```bash
docker compose -f docker-compose.tsdb.yml up -d
```

3. Review or edit device configuration

- sample device config: [src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json](src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json)
- app config: [src/DataAcquisition.Edge.Agent/appsettings.json](src/DataAcquisition.Edge.Agent/appsettings.json)

4. Start the Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

Validate configuration without starting acquisition:

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

If you also need the central service:

```bash
dotnet run --project src/DataAcquisition.Central.Api
```

## Documentation

Primary entry:

- [docs/index.en.md](docs/index.en.md)

Recommended path:

- [Getting Started](docs/tutorial-getting-started.en.md)
- [Configuration](docs/tutorial-configuration.en.md)
- [Deployment](docs/tutorial-deployment.en.md)
- [Driver Catalog](docs/hsl-drivers.en.md)
- [Design](docs/design.en.md)
- [Development](docs/tutorial-development.en.md)

## Development

Useful commands:

```bash
dotnet build DataAcquisition.sln --no-restore
dotnet test tests/DataAcquisition.Core.Tests/DataAcquisition.Core.Tests.csproj --no-build
```

If you want to extend PLC drivers, start with:

- [CONTRIBUTING.en.md](CONTRIBUTING.en.md)
- [docs/tutorial-development.en.md](docs/tutorial-development.en.md)

## License

This project is licensed under the [MIT License](LICENSE).
