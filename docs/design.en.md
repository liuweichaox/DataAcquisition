# Design

This document explains the project's core design principles, system boundaries, and engineering tradeoffs rather than providing file-by-file implementation details.

## 1. Product Boundary

The main product in this repository is the `Edge Agent`.

The project primarily addresses the following concerns:

- how to connect to PLCs
- how to collect data
- how to turn device reads into consistent acquisition messages
- how to write batches into TSDB reliably
- how to make field failures easy to diagnose

`Central API` and `Central Web` are support components used for:

- edge registration
- heartbeat and status inspection
- metrics and log proxying

They are not required to keep the acquisition path working.

## 2. Main Data Path

The runtime path is:

```text
PLC -> Collector -> Queue -> Storage
```

Meaning:

1. `Collector`
   reads data from PLCs according to device and channel configuration.

2. `Queue`
   batches messages by `plcCode + channelCode + measurement` and triggers writes.

3. `Storage`
   is currently InfluxDB by default.

The current runtime no longer includes a local WAL, replay folders, or a background replay worker.

When storage writes fail:

- the current batch is logged as failed
- the current batch is dropped
- later batches continue trying to write

That behavior is an explicit product decision, not an unfinished placeholder.

## 3. Why Direct-to-TSDB

The current project targets real-time acquisition rather than durable edge-side recovery.

The direct-to-TSDB decision is driven by:

- a simpler runtime model
- clearer success semantics, where success means storage confirmed the write
- avoiding local backlog replay that can distort time-series write ordering
- alignment with the project's current business priority, which is visibility of failure rather than local compensation

The resulting contract is:

- a sample is considered persisted only when storage succeeds
- failures surface through logs and metrics
- the project does not claim local replay, durable edge buffering, or delayed compensation

If your environment requires long offline retention or strict backfill, that is outside the current project boundary.

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

The runtime core does not depend directly on a specific PLC library. Instead, it depends on:

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
- TSDB writes and queries remain easier to reason about
- local timezone and DST issues do not leak into storage semantics

If local display time is needed, it should be handled in the UI or query layer.

## 8. State Recovery

Conditional acquisition depends not only on the current register value but also on the current active cycle state.

To survive process restarts, active cycle state is stored as:

- in-memory hot state
- SQLite-backed local recovery state

This allows the runtime to recover context and emit recovery diagnostics instead of silently forgetting state.

This recovery applies to conditional acquisition context only, not raw data replay.

## 9. Layering

The current layering is:

- `Domain`
  domain, message, and configuration models
- `Application`
  runtime abstractions and contracts
- `Infrastructure`
  drivers, acquisition, storage, logging, and metrics
- `Edge.Agent`
  main runtime entry
- `Central.Api` / `Central.Web`
  optional central support components

The goal is not academic purity. The goal is:

- keep default implementations inside Infrastructure
- keep extension points stable in Application
- keep runtime entry points simple

## 10. Design Principles

The current architecture is built around these principles:

- `Edge First`
- `Real-Time First`
- `Configuration Before Runtime`
- `Explicit Driver Contracts`
- `Observability First`
- `Formal Events Separate From Diagnostics`

These principles matter more than whether one class is split into three files.

## 11. Near-Term Direction

The architecture is already stable enough. The next valuable improvements are:

1. more real-world example configs
2. more end-to-end tests
3. fuller `ProtocolOptions` support for the most common drivers
4. stronger troubleshooting and operations docs
5. better central observability and diagnostics workflows
