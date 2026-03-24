# Development

This guide is for developers who want to change the runtime, add drivers, or replace default implementations.

If you only want to run the system, start with:

- [Getting Started](tutorial-getting-started.en.md)
- [Configuration](tutorial-configuration.en.md)

## Where to Start Reading the Code

Recommended order:

1. `src/DataAcquisition.Edge.Agent/Program.cs`
2. `src/DataAcquisition.Infrastructure/DataAcquisitions/DataAcquisitionService.cs`
3. `src/DataAcquisition.Infrastructure/Queues/QueueService.cs`
4. `src/DataAcquisition.Infrastructure/Clients/HslStandardPlcDriverProvider.cs`
5. `src/DataAcquisition.Infrastructure/DataStorages/InfluxDbDataStorageService.cs`

If you want the architectural picture first, read:

- [Modules](modules.en.md)
- [Design](design.en.md)

## Add a PLC Driver

Do not extend the runtime by piling more `switch` branches into a factory.

The recommended path is:

1. implement a new `IPlcDriverProvider`
2. reuse `PlcClientServiceBase` when it fits, or provide a new `IPlcClientService`
3. register the provider in the host
4. document the new `Driver` and provide an example config

Follow these rules:

- keep the `Driver` name stable and explicit
- be honest about whether `Host` and `Port` are used
- expose only real `ProtocolOptions`
- do not silently accept unused configuration
- do not leak driver-private logic into the upper acquisition flow

## Extend Storage

The project keeps primary storage and WAL separate on purpose.

### Replace the Primary Store

Implement:

- `IDataStorageService`

The main entry point is:

- `SaveBatchAsync(List<DataMessage>)`

### Replace WAL

Implement:

- `IWalStorageService`

The implementation must still model the WAL lifecycle:

- write
- read
- delete
- move to `retry/`
- enumerate replay files
- quarantine poison messages

When extending storage:

- do not bypass `QueueService`
- do not collapse the `pending/retry/invalid` lifecycle semantics

## Change Acquisition Logic

If you are changing runtime acquisition behavior, understand these boundaries first:

- `HeartbeatMonitor` decides whether a PLC is readable
- `ChannelCollector` owns channel-level orchestration
- `ChannelMetricReader` reads fields
- `MetricExpressionEvaluator` evaluates expressions
- `AcquisitionStateManager` owns conditional recovery state

Do not merge:

- low-level PLC reads
- cycle semantics
- storage persistence

back into a single class.

## Change the Configuration System

The current configuration system is designed to be:

- JSON-based
- hot-reload friendly
- offline-validatable
- explicit about driver contracts

If you extend it, try to preserve:

- stable top-level fields
- protocol-specific differences pushed into `ProtocolOptions`
- validation rules that evolve together with documentation

## Testing Expectations

If you add new behavior, add at least one of:

- unit tests
- integration tests
- configuration validation tests

The highest-value coverage areas are:

- driver configuration contracts
- WAL behavior
- recovery semantics

## Before You Submit

Before opening a PR, run at least:

```bash
dotnet build DataAcquisition.sln --no-restore
dotnet test tests/DataAcquisition.Core.Tests/DataAcquisition.Core.Tests.csproj
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

## Related Docs

- [Contributing](../CONTRIBUTING.en.md)
- [Modules](modules.en.md)
- [Design](design.en.md)
