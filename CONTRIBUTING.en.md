# Contributing Guide

Thanks for your interest in DataAcquisition.

The project is acquisition-first. Its primary goal is not to become a feature-heavy central platform, but to make the PLC acquisition path solid: stable connections, correct reads, WAL-first persistence, recoverable failures, and auditable behavior. Contributions should reinforce that path.

## Principles First

- The Edge Agent is the main product; Central API / Web are diagnostics and management helpers
- Do not bypass `QueueService` and write directly into the primary store
- Do not reintroduce `Type + switch` style hard-coded factories
- Extend PLC protocols through `IPlcDriverProvider`
- Keep configuration stable through full `Driver` names
- Update docs and examples together with code

## Local Development

```bash
dotnet restore
dotnet test tests/DataAcquisition.Core.Tests/DataAcquisition.Core.Tests.csproj
dotnet build DataAcquisition.sln --no-restore
```

For end-to-end local validation, prepare:

- InfluxDB 2.x
- `src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json`
- `src/DataAcquisition.Simulator`

## Contribution Areas

Useful contributions include:

- new PLC drivers or improvements to existing ones
- reliability fixes in the acquisition path
- WAL / retry / restart-recovery improvements
- docs, configuration examples, tutorials
- automated tests

## Adding a New PLC Driver

Recommended path:

1. Prefer inheriting `PlcClientServiceBase`, or implement `IPlcConnectionClient` / `IPlcDataAccessClient` / `IPlcTypedWriteClient` as needed
2. Implement an `IPlcDriverProvider`
3. Register the provider through DI
4. Add docs and sample configuration for the new driver
5. Add tests for important configuration behavior

Requirements:

- use a stable `Driver` name
- do not introduce alias systems
- keep the `Host` / `Port` contract honest and never ignore endpoint config silently
- only expose `ProtocolOptions` that the driver truly supports
- fail explicitly for unsupported `ProtocolOptions`

## Extending Storage Backends

- Replace the primary store by implementing `IDataStorageService.SaveBatchAsync`
- Replace the WAL backend by implementing `IWalStorageService`

Requirements:

- keep the WAL-first contract intact
- preserve lifecycle boundaries such as `pending/retry/invalid`
- poison messages must remain auditable, not silently dropped

## Before Opening a PR

At minimum, make sure that:

- the code builds
- relevant tests pass
- new behavior has at least minimal automated coverage
- README / tutorials / sample configs are updated
- local runtime artifacts are not committed

## Recommended PR Content

PR descriptions should include:

- problem statement
- design trade-offs
- risks
- validation steps
- migration notes if configuration behavior changes

## Changes We Intentionally Avoid

- forcing unlike PLC protocols into an oversized “unified” model
- bloating the shared configuration model for one driver-specific need
- adding configuration without tests and docs
- making claims the implementation cannot prove, such as absolute “zero data loss”
