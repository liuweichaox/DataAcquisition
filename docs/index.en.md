# Documentation Home

This documentation set describes how to configure, deploy, operate, understand, and extend DataAcquisition.

For faster navigation, it is organized by usage goal rather than as a loose collection of isolated notes.

## Recommended Reading Order

If this is your first time with the project, read in this order:

1. [Getting Started](tutorial-getting-started.en.md)
2. [Configuration](tutorial-configuration.en.md)
3. [Driver Catalog](hsl-drivers.en.md)
4. [Deployment](tutorial-deployment.en.md)

## Read by Usage Goal

### Local Startup and Integration

- [Getting Started](tutorial-getting-started.en.md)
- [Configuration](tutorial-configuration.en.md)

### Connecting a Real PLC

- [Configuration](tutorial-configuration.en.md)
- [Driver Catalog](hsl-drivers.en.md)
- [FAQ](faq.en.md)

### Deployment and Operations

- [Deployment](tutorial-deployment.en.md)
- [FAQ](faq.en.md)

### Architecture and Module Understanding

- [Design](design.en.md)
- [Modules](modules.en.md)

### Extension and Contribution

- [Development](tutorial-development.en.md)
- [Contributing](../CONTRIBUTING.en.md)

## Core Constraints

Before going deeper, keep these rules in mind:

- the `Edge Agent` is the main product
- `Central` is an optional control plane
- the main path is `PLC -> Collector -> Queue -> TSDB`
- queue batches write directly to storage without a local WAL or replay worker
- drivers are selected by stable `Driver` names
- configuration must be validated before runtime
- formal business events and recovery diagnostics are stored separately

## Documentation Set

The documentation tree intentionally keeps only the core set:

- [Getting Started](tutorial-getting-started.en.md)
- [Configuration](tutorial-configuration.en.md)
- [Driver Catalog](hsl-drivers.en.md)
- [Deployment](tutorial-deployment.en.md)
- [Design](design.en.md)
- [Modules](modules.en.md)
- [Development](tutorial-development.en.md)
- [FAQ](faq.en.md)
