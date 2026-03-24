# FAQ

This FAQ answers recurring questions without repeating the full tutorials.

If you are new to the project, start with:

- [Documentation Index](index.en.md)
- [Getting Started](tutorial-getting-started.en.md)
- [Configuration](tutorial-configuration.en.md)

## Project Scope

### What is DataAcquisition

It is a PLC data collection runtime.

It is responsible for:

- reading values from PLCs
- producing normalized acquisition messages
- writing local WAL first
- writing primary storage second
- exposing local diagnostics

### What DataAcquisition is not

It is not:

- a PLC programming tool
- a SCADA system
- an MES
- a time-series database

The central UI is an auxiliary control plane, not the acquisition path itself.

## Configuration and Drivers

### Which driver name should I use

Use the exact `Driver` names listed in the [driver catalog](hsl-drivers.en.md).

Do not rely on old aliases, abbreviations, or guessed names.

### Why does configuration validation fail

Common reasons:

- invalid JSON
- missing required fields
- empty `PlcCode`
- duplicated `PlcCode` across files
- `Driver` not found in the built-in catalog
- unsupported keys in `ProtocolOptions`

Run this first:

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

### Do I need to restart after editing config

Usually no.

The device configuration directory is watched for file changes, and valid configuration updates are reloaded automatically.

The important condition is:

- the new configuration must pass validation first

### How do I add a new PLC protocol

If the built-in catalog does not cover your protocol, add a provider.

Recommended extension path:

1. implement a new `IPlcDriverProvider`
2. reuse `PlcClientServiceBase` or provide your own `IPlcClientService`
3. register the provider at startup
4. document the new `Driver` and provide a config example

If you only use the built-in Hsl drivers, no core change is usually required.

## Acquisition and Storage

### Why write WAL first

Because primary storage may fail, and edge collection cannot depend on InfluxDB being immediately available.

WAL-first means:

- persist data locally first
- attempt primary storage next
- replay later if primary storage failed

### What is the difference between `pending`, `retry`, and `invalid`

- `pending/`: WAL files just written and not yet finalized against primary storage
- `retry/`: WAL files waiting for replay after primary storage failure
- `invalid/`: poison messages that could not be written to WAL

These are not redundant folders. They describe lifecycle state and separate the real-time path from the replay path.

### Why are there many WAL files

Usually because primary storage is failing or unreachable.

Check in this order:

1. whether `retry/` keeps growing
2. whether InfluxDB is reachable
3. the Edge logs for storage failures
4. `InfluxDB:Url`, bucket, org, and token

### What does it mean if `invalid/` contains files

It means some messages are poison messages and cannot be serialized into WAL.

They have been isolated and will not keep blocking healthy messages.

The right response is:

- inspect the related log entry
- locate the bad field or configuration
- fix the source of the poison message

### Does InfluxDB downtime stop collection

It should stop primary storage writes, but it should not immediately stop the collection path itself.

Expected behavior:

- new data still goes to local WAL
- `retry/` accumulates
- replay catches up after InfluxDB recovers

If InfluxDB is down and WAL is not being written either, that is a failure, not expected behavior.

## Cycle Collection

### Does the first conditional sample trigger a fake edge

No.

Current behavior:

- the first sample builds a baseline
- initialization is not treated as a real edge event

### Why do I see `RecoveredStart` or `Interrupted`

These are recovery diagnostics emitted when the process restarts or resumes during an active cycle.

They should not be used as the formal business definition of a complete cycle.

For formal cycle analytics, still use paired `Start` / `End`.

### Why store active cycle state at all

Because conditional acquisition needs restart-time context recovery.

The active cycle is mirrored to:

- memory
- `Data/acquisition-state.db`

The purpose is not to invent missing cycles. It is to preserve recovery context for an in-progress cycle.

## Operations and Troubleshooting

### How do I know the system is healthy

Start with:

```bash
curl http://localhost:8001/health
curl http://localhost:8001/metrics
```

Then verify:

- whether `retry/` keeps growing
- whether `invalid/` receives files
- whether InfluxDB is receiving measurements

### Why is host-process deployment recommended for Edge

Because PLC networking problems are usually real network problems:

- NIC selection
- routes
- VLANs
- firewalls
- device reachability

That is easier to troubleshoot with a host process than with a containerized edge runtime.

Central components and InfluxDB are better containerization candidates.

### What happens if Central is down

If Central is unavailable:

- registration and heartbeat reporting fail
- the central UI is unavailable

But the collection path should continue running.

## Extension and Development

### How do I replace the primary store

Implement `IDataStorageService` and replace the default registration in the host.

### How do I replace WAL

Implement `IWalStorageService` and preserve explicit lifecycle semantics.

At minimum, WAL should still model:

- newly written files
- replay-pending files
- poison-message quarantine

### Why does the project use JSON configuration

Because the goal here is:

- simple
- readable
- hot-reload friendly
- easy to validate and bind in .NET

What matters more than switching to YAML or TOML is:

- a stable config contract
- validation
- examples
- good error messages

## Related Docs

- [Configuration](tutorial-configuration.en.md)
- [Deployment](tutorial-deployment.en.md)
- [Driver Catalog](hsl-drivers.en.md)
- [Design](design.en.md)
