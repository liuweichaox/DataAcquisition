# Design

This document explains the core design decisions of the project, not file-by-file implementation details.

## 1. Product Boundary

The main product in this repository is the `Edge Agent`.

That means the project primarily solves:

- how to connect to PLCs
- how to collect data
- how to persist locally before remote success
- how to recover when primary storage fails

`Central API` and `Central Web` are support components used for:

- edge registration
- heartbeat and status inspection
- metrics and log proxying

They are not required to keep the acquisition path working.

## 2. Main Data Path

The runtime path is:

```text
PLC -> Collector -> Queue -> Parquet WAL -> Primary Storage
```

Meaning:

1. `Collector`
   reads data from PLCs according to device and channel configuration.

2. `Queue`
   handles batching, flushes, and in-memory requeue on failure.

3. `Parquet WAL`
   is the local recoverable copy.

4. `Primary Storage`
   is currently InfluxDB by default.

When primary storage fails:

- WAL files are moved into `retry/`
- a background worker retries them later

When a message itself cannot be written into WAL:

- the single poisoned message goes into `invalid/`
- healthy messages in the batch keep moving

## 3. Why WAL-first

Primary storage failure is a normal edge scenario, not an exceptional one.

Examples:

- InfluxDB is down
- the network is temporarily unavailable
- the configured endpoint is wrong

If the runtime writes directly to primary storage, the edge node loses data in exactly these scenarios.  
That is why the runtime uses:

- local WAL first
- primary storage second

## 4. Configuration Model

The project uses JSON device configuration instead of a database-backed config center.

Reasons:

- easier edge deployment
- simple file-based operations
- natural integration with the .NET configuration model
- better fit for industrial single-node operations

The core config fields are:

- `Driver`
- `Host`
- `Port`
- `ProtocolOptions`
- `Channels`

The design rule is:

- keep top-level fields stable
- push protocol-specific differences into `ProtocolOptions`
- validate configs before runtime

## 5. Driver Model

Drivers are selected through stable `Driver` names such as:

- `melsec-a1e`
- `melsec-mc`
- `siemens-s7`

The runtime core does not depend directly on a specific PLC library.  
Instead, it depends on:

- `IPlcDriverProvider`
- `IPlcClientService`

The current built-in implementation uses HslCommunication, but that is a default implementation choice, not an architectural requirement.

## 6. Acquisition Modes

The runtime supports two acquisition modes.

### Always

For continuous signals and real-time values.

### Conditional

For cycle boundaries, steps, and edge-triggered events.

In conditional mode:

- formal business events are written as `Start` / `End`
- recovery diagnostics are written to `<measurement>_diagnostic`

This keeps recovery semantics out of formal cycle analytics.

## 7. Time Semantics

The runtime uses UTC timestamps internally.

Why:

- multiple edge nodes can be compared on one timeline
- WAL replay and retry have one unambiguous time meaning
- local timezone and DST issues do not leak into storage semantics

If local display time is needed, it should be handled in the UI or query layer.

## 8. State Recovery

Conditional acquisition depends not only on the current register value but also on the current active cycle state.

To survive process restarts, active cycle state is stored as:

- in-memory hot state
- SQLite-backed local recovery state

This allows the runtime to recover context and emit recovery diagnostics instead of silently forgetting state.

## 9. Layering

The current layering is:

- `Domain`
  domain, message, and configuration models
- `Application`
  runtime abstractions and contracts
- `Infrastructure`
  drivers, acquisition, storage, WAL, logging, and metrics
- `Edge.Agent`
  main runtime entry
- `Central.Api` / `Central.Web`
  optional central support components

The goal is not academic purity.  
The goal is:

- keep default implementations inside Infrastructure
- keep extension points stable in Application
- keep runtime entry points simple

## 10. Design Principles

The current architecture is built around these principles:

- `Edge First`
- `WAL First`
- `Configuration Before Runtime`
- `Explicit Driver Contracts`
- `Formal Events Separate From Diagnostics`

These principles matter more than whether one class is split into three files.

## 11. Current Direction

The architecture is already stable enough.  
The next valuable improvements are:

1. more real-world example configs
2. more end-to-end tests
3. fuller `ProtocolOptions` support for the most common drivers
4. stronger troubleshooting and operations docs
