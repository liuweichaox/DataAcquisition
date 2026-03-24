# DataAcquisition

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

中文: [README.md](README.md)

DataAcquisition is an open-source PLC data acquisition runtime for industrial edge environments.

Its core job is intentionally narrow:

- read data from PLCs reliably
- persist locally before writing to primary storage
- make acquisition jobs easy to configure and operate

This repository is designed as a deployable edge runtime, not as a full MES or SCADA platform.

## What It Is For

- shop-floor PLC data acquisition
- local WAL buffering and retry on edge nodes
- writing time-series data into InfluxDB
- condition-triggered cycle acquisition
- operating multiple PLCs with explicit configuration and diagnostics

## What It Is Not For

- replacing a full MES / SCADA stack
- hiding every vendor-specific detail behind one universal model
- exposing every private driver setting as a global configuration field

## Architecture

Main data path:

```text
PLC -> Collector -> Queue -> Parquet WAL -> Primary Storage
                           |               |
                           |               -> retry/
                           -> invalid/
```

Deployment topology:

```text
Central Web -> Central API -> Edge Agent -> PLC
```

Design priorities:

- `Edge First`: the Edge Agent is the main product, Central is optional support
- `WAL First`: local recoverability comes before primary storage success
- `Driver + Provider`: simple protocol selection with explicit extension points
- `UTC`: acquisition timestamps use UTC semantics across nodes

## Quick Start

Prerequisites:

- .NET 10 SDK
- InfluxDB 2.x
- Docker, if you want to use the provided compose file for InfluxDB

1. Build the solution

```bash
dotnet build DataAcquisition.sln
```

2. Start InfluxDB

```bash
docker compose -f docker-compose.tsdb.yml up -d
```

3. Review configuration

- sample device config: [src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json](src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json)
- example configs: [examples/device-configs](examples/device-configs)
- JSON Schema: [schemas/device-config.schema.json](schemas/device-config.schema.json)

4. Validate configs offline

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

To validate another directory:

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs --config-dir ./examples/device-configs
```

5. Start the Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

For local development, you can also run the simulator:

```bash
dotnet run --project src/DataAcquisition.Simulator
```

## Configuration Model

Device configuration uses JSON.

Minimal structure:

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

Configuration rules:

- `Driver` accepts stable full names only
- `Host` accepts IPs and DNS hostnames
- `ProtocolOptions` must match the selected driver
- the default config directory comes from `Acquisition:DeviceConfigService:ConfigDirectory`

Full details: [docs/tutorial-configuration.en.md](docs/tutorial-configuration.en.md)

## Driver Model

The default built-in driver implementation uses HslCommunication, but the runtime core does not depend on Hsl-specific types.

Built-in drivers are selected by stable `Driver` names such as:

- `melsec-a1e`
- `melsec-mc`
- `siemens-s7`
- `omron-fins`
- `inovance-tcp`
- `beckhoff-ads`

See [docs/hsl-drivers.en.md](docs/hsl-drivers.en.md) for the current catalog and protocol options.

If you want to add your own PLC driver, start here:

- [IPlcDriverProvider](src/DataAcquisition.Application/Abstractions/IPlcDriverProvider.cs)
- [IPlcClientService](src/DataAcquisition.Application/Abstractions/IPlcClientService.cs)
- [CONTRIBUTING.en.md](CONTRIBUTING.en.md)

## Documentation

Documentation home:

- [docs/index.en.md](docs/index.en.md)

Suggested reading path:

- [Getting Started](docs/tutorial-getting-started.en.md)
- [Configuration](docs/tutorial-configuration.en.md)
- [Driver Catalog](docs/hsl-drivers.en.md)
- [Deployment](docs/tutorial-deployment.en.md)
- [Design](docs/design.en.md)
- [Development](docs/tutorial-development.en.md)
- [FAQ](docs/faq.en.md)

## Repository Layout

- [src/DataAcquisition.Edge.Agent](src/DataAcquisition.Edge.Agent)
  main edge runtime
- [src/DataAcquisition.Infrastructure](src/DataAcquisition.Infrastructure)
  acquisition, drivers, WAL, storage, logging, and metrics
- [src/DataAcquisition.Application](src/DataAcquisition.Application)
  abstractions and runtime contracts
- [src/DataAcquisition.Domain](src/DataAcquisition.Domain)
  domain and configuration models
- [src/DataAcquisition.Central.Api](src/DataAcquisition.Central.Api)
  central API
- [src/DataAcquisition.Central.Web](src/DataAcquisition.Central.Web)
  central web UI
- [tests/DataAcquisition.Core.Tests](tests/DataAcquisition.Core.Tests)
  core tests

## Development

Useful commands:

```bash
dotnet build DataAcquisition.sln --no-restore
dotnet test tests/DataAcquisition.Core.Tests/DataAcquisition.Core.Tests.csproj
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

## License

This project is licensed under the [MIT License](LICENSE).
