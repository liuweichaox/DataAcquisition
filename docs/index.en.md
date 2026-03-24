# Documentation Home

These docs are organized around one goal: help you run DataAcquisition as a real PLC acquisition runtime, deploy it, understand it, and extend it.

Do not treat the docs as a loose collection of notes.  
The intended experience is goal-based reading.

## Start Here

If this is your first time with the project, read in this order:

1. [Getting Started](tutorial-getting-started.en.md)
2. [Configuration](tutorial-configuration.en.md)
3. [Driver Catalog](hsl-drivers.en.md)
4. [Deployment](tutorial-deployment.en.md)

## Read by Goal

### I just want it running

- [Getting Started](tutorial-getting-started.en.md)
- [Configuration](tutorial-configuration.en.md)

### I want to connect a real PLC

- [Configuration](tutorial-configuration.en.md)
- [Driver Catalog](hsl-drivers.en.md)
- [FAQ](faq.en.md)

### I want to deploy edge nodes

- [Deployment](tutorial-deployment.en.md)
- [FAQ](faq.en.md)

### I want to understand the architecture

- [Design](design.en.md)
- [Modules](modules.en.md)

### I want to extend the project

- [Development](tutorial-development.en.md)
- [Contributing](../CONTRIBUTING.en.md)

## Core Conventions

Before going deeper, keep these rules in mind:

- the `Edge Agent` is the main product
- `Central` is an optional control plane
- the main path is `PLC -> Collector -> Queue -> Parquet WAL -> Primary Storage`
- drivers are selected by stable `Driver` names
- configuration must be validated before runtime
- formal business events and recovery diagnostics are stored separately

## Current Docs Set

The documentation tree intentionally keeps only the core set:

- [Getting Started](tutorial-getting-started.en.md)
- [Configuration](tutorial-configuration.en.md)
- [Driver Catalog](hsl-drivers.en.md)
- [Deployment](tutorial-deployment.en.md)
- [Design](design.en.md)
- [Modules](modules.en.md)
- [Development](tutorial-development.en.md)
- [FAQ](faq.en.md)
