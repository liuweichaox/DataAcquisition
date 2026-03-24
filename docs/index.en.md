# Documentation Home

These docs are organized around one goal: help you run a real PLC acquisition node and understand its boundaries, configuration model, and extension points.

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
- [Data Flow](data-flow.en.md)
- [FAQ](faq.en.md)

### I want to understand the design

- [Design](design.en.md)
- [Modules](modules.en.md)
- [Data Flow](data-flow.en.md)

### I want to extend the project

- [Development](tutorial-development.en.md)
- [Contributing](../CONTRIBUTING.en.md)

## Reference Docs

- [API Usage](api-usage.en.md)
- [Data Query](tutorial-data-query.en.md)
- [Performance](performance.en.md)
- [Docker InfluxDB Setup](docker-influxdb.en.md)

## Project Conventions

Before going deeper, keep these conventions in mind:

- the `Edge Agent` is the main product; `Central` is optional support
- the main path is `PLC -> Collector -> Queue -> Parquet WAL -> Primary Storage`
- drivers are selected by stable `Driver` names
- configs should be validated before runtime
- formal business events and recovery diagnostics are stored separately
