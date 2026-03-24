# Design

This document explains the project’s core positioning, architectural boundaries, and intentional trade-offs.

## Project Positioning

DataAcquisition is not designed as a “central industrial platform”.  
Its core is an industrial PLC data acquisition runtime.

The project prioritizes:

- reliable PLC connectivity
- correct register acquisition
- recoverable local persistence first
- primary storage after local durability
- auditable behavior across restart, network loss, and primary-store failure

Because of that, the Edge Agent is the main product. Central API and Central Web are supporting control-plane and diagnostics components.

## Core Principles

## 1. Acquisition First

The Edge Agent must be able to run without Central API and still provide:

- PLC connection management
- channel acquisition
- WAL persistence
- primary storage writes
- retry behavior

The central side is responsible for:

- node registration and heartbeat
- edge metrics and log proxying
- centralized diagnostics and visualization

The central side is not part of the primary acquisition path.

## 2. WAL Before Primary Storage

The main data path is:

`PLC -> ChannelCollector -> QueueService -> WAL -> TSDB`

WAL is the safety boundary, not an optional side effect.

The WAL lifecycle is modeled explicitly:

- `pending/`: newly written, not yet finalized against primary storage
- `retry/`: primary storage failed, waiting for replay
- `invalid/`: poison messages that could not be written into WAL

The goal is not to make unrealistic “absolute zero-loss” promises. The goal is to make failures understandable and recoverable:

- healthy messages are persisted locally first
- primary storage failures are replayable
- poison messages do not block healthy batches
- failure behavior is explicit and auditable

## 3. Explicit Runtime Contracts

The framework separates runtime contracts from default implementations:

- `IPlcDriverProvider`
- `IPlcClientService`
- `IPlcConnectionClient`
- `IPlcDataAccessClient`
- `IPlcTypedWriteClient`
- `IDataStorageService`
- `IWalStorageService`
- `IQueueService`
- `IChannelCollector`
- `IAcquisitionStateManager`

This means:

- HslCommunication is the default PLC implementation, not an architectural requirement
- InfluxDB is the default primary store, not the only possible one
- Parquet is the default WAL implementation, not a hard framework dependency

## 4. Restart Recovery Is a Feature

Industrial collectors must assume that processes restart.

Current recovery behavior:

- active cycles are stored in memory and mirrored to SQLite
- the first conditional sample only establishes baseline state and does not fake `Start/End`
- restart recovery may emit diagnostics when needed
- recovery diagnostics go to `<measurement>_diagnostic` so they do not pollute the formal cycle measurement

## 5. Honest Configuration Contracts

Configuration is intentionally small and explicit.

Stable public fields:

- `Driver`
- `Host`
- `Port`
- `ProtocolOptions`
- `Channels`

Rules:

- drivers accept stable full names only
- `Host` and `Port` are public endpoint contracts and must not be silently ignored
- `ProtocolOptions` expose only options actually supported by the current driver
- unsupported `ProtocolOptions` are rejected at runtime

## Runtime Structure

### Edge Main Path

1. `DeviceConfigService`
   - loads configs and handles hot reload
2. `PlcClientLifecycleService`
   - creates and manages PLC clients from `Driver`
3. `HeartbeatMonitor`
   - tracks connection health
4. `ChannelCollector`
   - runs `Always` and `Conditional` acquisition
5. `QueueService`
   - batches messages and drives persistence
6. `QueueBatchPersister`
   - executes the WAL-first persistence path
7. `ParquetRetryWorker`
   - replays files in `retry/`
8. `InfluxDbDataStorageService`
   - default primary store

### Central Side

The Central API belongs to the control and diagnostics plane:

- `EdgeRegistry`: registration and heartbeat state
- `EdgeDiagnosticsController`: log and metrics proxying
- `MetricsController`: central metrics observation

## PLC Driver Design

The driver layer is one of the most important open-source extension points in the project.

Current model:

- configuration uses stable `Driver` names
- the default catalog is provided by `HslStandardPlcDriverProvider`
- the framework core does not depend directly on Hsl-specific types
- third parties can integrate other protocol stacks through new `IPlcDriverProvider` implementations
- driver implementations can reuse `PlcClientServiceBase` instead of building a large client implementation from scratch

This gives:

- regular users a configuration-first experience for common PLCs
- advanced users a clear extension point for custom drivers

## Intentionally Simple Areas

The project deliberately avoids over-engineering in a few places:

- the Hsl driver catalog remains a single-file registry
- `ProtocolOptions` is not a heavy metadata framework
- recovery diagnostics move to a sibling measurement rather than a separate event bus

These are intentional trade-offs to keep the project:

- easy to adopt
- easy to read
- easy to extend
- easy to maintain

## Recommended Evolution

If the project keeps moving toward a more mature open-source shape, the suggested order is:

1. add more automated tests around the main acquisition path
2. extend `ProtocolOptions` for the most important drivers
3. keep tightening the boundary between formal cycle analytics and recovery diagnostics in docs and query examples
4. add more real example configurations and troubleshooting guides

## Related Docs

- [Data Flow](data-flow.en.md)
- [Core Modules](modules.en.md)
- [Driver Catalog](hsl-drivers.en.md)
