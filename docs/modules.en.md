# 📦 Core Modules

This document explains the current modules and responsibilities through the lens of the PLC acquisition runtime path.

## Module Overview

### 1. PLC Driver Layer

Location:

- `src/DataAcquisition.Application/Abstractions/IPlcClientService.cs`
- `src/DataAcquisition.Application/Abstractions/IPlcClientFactory.cs`
- `src/DataAcquisition.Infrastructure/Clients/HslStandardPlcDriverProvider.cs`
- `src/DataAcquisition.Infrastructure/Clients/HslPlcClientService.cs`

Responsibilities:

- select PLC communication implementations through stable `Driver` names
- keep upper layers decoupled from direct HslCommunication usage
- validate and apply `ProtocolOptions`

Current default implementation:

- `HslStandardPlcDriverProvider`
- `HslPlcClientService`

### 2. Acquisition Orchestration Layer

Location:

- `src/DataAcquisition.Infrastructure/DataAcquisitions/DataAcquisitionService.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/HeartbeatMonitor.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/ChannelCollector.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/AcquisitionStateManager.cs`

Responsibilities:

- start acquisition tasks per device and channel
- manage PLC health and connectivity
- run Always / Conditional acquisition
- manage active cycles and recover them after restart

This is the core of the PLC acquisition path.

### 3. Queue and Persistence Layer

Location:

- `src/DataAcquisition.Infrastructure/Queues/QueueService.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/ParquetFileStorageService.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/InfluxDbDataStorageService.cs`

Responsibilities:

- batch and aggregate data messages
- write WAL first, then write primary storage
- retry failed primary writes
- quarantine poison messages into `invalid/`

Key directories:

- `pending/`
- `retry/`
- `invalid/`

### 4. Configuration and Operability Layer

Location:

- `src/DataAcquisition.Infrastructure/DeviceConfigs/DeviceConfigService.cs`
- `src/DataAcquisition.Infrastructure/Metrics/*`
- `src/DataAcquisition.Infrastructure/Logs/*`

Responsibilities:

- device configuration loading and hot reload
- Prometheus metrics
- SQLite-backed log querying

### 5. Host Layer

Location:

- `src/DataAcquisition.Edge.Agent/Program.cs`
- `src/DataAcquisition.Edge.Agent/BackgroundServices/*`
- `src/DataAcquisition.Central.Api/*`
- `src/DataAcquisition.Central.Web/*`

Responsibilities:

- Edge Agent: acquisition host, background workers, local diagnostics API
- Central API: registration, heartbeat, diagnostics proxy
- Central Web: centralized status and metrics UI

## Main Runtime Path

### Always / Conditional Data Acquisition

Main flow:

1. `DataAcquisitionService` starts acquisition loops per device/channel
2. `HeartbeatMonitor` checks whether PLC reads are allowed
3. `ChannelCollector` reads values from PLC
4. a `DataMessage` is created
5. the message is sent to `QueueService`
6. `QueueService` writes WAL first, then writes primary storage

### Conditional Recovery

Conditional acquisition additionally depends on:

- `AcquisitionStateManager`

Current behavior:

- active cycles are stored in memory and mirrored to SQLite
- the first sample builds a baseline instead of faking a normal `Start/End`
- restart diagnostics may emit `RecoveredStart` / `Interrupted`
- recovery diagnostics are written to `<measurement>_diagnostic`
- acquisition messages use UTC timestamps

For formal cycle analytics, only paired `Start` / `End` should be treated as complete cycles.

## Main Extension Points

### Add a New PLC Driver

1. implement a new `IPlcDriverProvider`
2. optionally add a new `IPlcClientService` implementation
3. register the provider in `Program.cs`
4. use the full `Driver` name in configuration

### Replace the Primary Store

1. implement `IDataStorageService`
2. replace the default registration in the Edge Agent

### Replace the WAL Backend

1. implement `IWalStorageService`
2. keep lifecycle semantics such as `pending/retry/invalid`

## Automated Tests

Test project:

- `tests/DataAcquisition.Core.Tests`

Current high-value coverage focuses on:

- driver configuration validation
- active cycle persistence and recovery
- WAL poison message isolation

## Related Docs

- [Architecture Design](design.en.md)
- [Data Flow](data-flow.en.md)
- [Configuration Tutorial](tutorial-configuration.en.md)
