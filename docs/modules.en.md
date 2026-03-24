# Modules

This document does not try to list every file. It explains the main runtime surfaces and the module boundaries of the project.

The primary product is `Edge Agent`.  
Everything else should be understood around that collection path.

## Module View

### Domain

Location:

- `src/DataAcquisition.Domain`

Responsibilities:

- define configuration models
- define acquisition messages
- define controlled value normalization rules
- define core models that do not depend on specific libraries

This layer should not know about Hsl, InfluxDB, SQLite, or ASP.NET.

### Application

Location:

- `src/DataAcquisition.Application`

Responsibilities:

- define runtime abstractions
- define PLC driver interfaces
- define storage contracts
- define configuration, queue, and acquisition contracts

This layer answers:

- what capabilities the system needs
- not how those capabilities are implemented

### Infrastructure

Location:

- `src/DataAcquisition.Infrastructure`

Responsibilities:

- provide default implementations
- wrap Hsl drivers
- wrap InfluxDB
- wrap Parquet WAL
- wrap SQLite logs and recovery state
- implement config hot reload, metrics, and diagnostics

This is the largest implementation layer, but it should not define the upper-level abstractions.

### Edge Agent

Location:

- `src/DataAcquisition.Edge.Agent`

Responsibilities:

- bootstrap the runtime
- register default implementations
- host background workers
- expose local health, metrics, logs, and diagnostics

If you only care about what the project actually does, start here.

### Central API / Central Web

Location:

- `src/DataAcquisition.Central.Api`
- `src/DataAcquisition.Central.Web`

Responsibilities:

- provide centralized visibility
- show heartbeats, metrics, and diagnostic proxies

These are part of the control plane, not the collection path itself.

### Tests

Location:

- `tests/DataAcquisition.Core.Tests`

Responsibilities:

- validate driver config contracts
- validate WAL behavior
- validate recovery logic
- validate configuration rules

## Main Runtime Path

The core runtime path is intentionally fixed:

1. `Edge Agent` starts
2. device configs are loaded
3. PLC drivers are created
4. heartbeat and acquisition tasks start
5. `DataMessage` instances are produced
6. messages enter `QueueService`
7. `Parquet WAL` is written first
8. `InfluxDB` is written second
9. failed primary writes move into `retry/`

This path is the baseline for judging whether module boundaries make sense.

## Key Modules

### PLC Driver Layer

Key files:

- `src/DataAcquisition.Application/Abstractions/IPlcDriverProvider.cs`
- `src/DataAcquisition.Application/Abstractions/IPlcClientService.cs`
- `src/DataAcquisition.Infrastructure/Clients/PlcClientFactory.cs`
- `src/DataAcquisition.Infrastructure/Clients/HslStandardPlcDriverProvider.cs`
- `src/DataAcquisition.Infrastructure/Clients/HslPlcClientService.cs`

Responsibilities:

- select concrete PLC implementations using stable `Driver` names
- keep upper layers decoupled from Hsl
- parse and apply driver-specific configuration

The important design decision here is:

- the framework is not coupled to a PLC type enum
- Hsl is the default implementation, not the architectural foundation

### Acquisition Orchestration Layer

Key files:

- `src/DataAcquisition.Infrastructure/DataAcquisitions/DataAcquisitionService.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/HeartbeatMonitor.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/ChannelCollector.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/ChannelMetricReader.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/MetricExpressionEvaluator.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/AcquisitionStateManager.cs`

Responsibilities:

- start and manage acquisition tasks
- decide whether a device is readable
- run Always / Conditional acquisition
- read fields, evaluate expressions, and produce events
- recover active cycle state for conditional acquisition

### Queue and Storage Layer

Key files:

- `src/DataAcquisition.Infrastructure/Queues/QueueService.cs`
- `src/DataAcquisition.Infrastructure/Queues/QueueBatchPersister.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/ParquetFileStorageService.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/ParquetDataMessageSerializer.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/InfluxDbDataStorageService.cs`

Responsibilities:

- batch messages
- write WAL first, primary storage second
- replay failed data
- quarantine poison messages

This is the most important safety boundary in the system.

### Configuration and Operability Layer

Key files:

- `src/DataAcquisition.Infrastructure/DeviceConfigs/DeviceConfigService.cs`
- `src/DataAcquisition.Infrastructure/DeviceConfigs/DeviceConfigValidator.cs`
- `src/DataAcquisition.Infrastructure/DeviceConfigs/DeviceConfigFileLoader.cs`
- `src/DataAcquisition.Infrastructure/Logs/*`
- `src/DataAcquisition.Infrastructure/Metrics/*`

Responsibilities:

- read and validate JSON configuration
- monitor the config directory and hot-reload it
- provide Prometheus metrics
- provide local log querying

## Boundary Rules

If I were maintaining this as a long-lived open source project, I would keep these rules:

- `Domain` does not depend on infrastructure libraries
- `Application` defines contracts only
- `Infrastructure` implements, but does not define upper-level business rules
- `Edge Agent` stays the primary product
- documentation only promises capabilities that are actually supported

## Extension Points

### Add a New PLC Driver

Path:

1. implement a new `IPlcDriverProvider`
2. provide a new `IPlcClientService` if needed
3. register it in the host
4. document the full `Driver` name and provide a config example

### Replace the Primary Store

Path:

1. implement `IDataStorageService`
2. replace the default host registration

### Replace WAL

Path:

1. implement `IWalStorageService`
2. preserve explicit lifecycle semantics

## Recommended Reading Order

If you want to understand the codebase quickly, read in this order:

1. `README.md`
2. `docs/design.md`
3. `src/DataAcquisition.Edge.Agent/Program.cs`
4. `src/DataAcquisition.Infrastructure/DataAcquisitions/DataAcquisitionService.cs`
5. `src/DataAcquisition.Infrastructure/Queues/QueueService.cs`
6. `src/DataAcquisition.Infrastructure/Clients/HslStandardPlcDriverProvider.cs`

## Related Docs

- [Design](design.en.md)
- [Configuration](tutorial-configuration.en.md)
- [Deployment](tutorial-deployment.en.md)
