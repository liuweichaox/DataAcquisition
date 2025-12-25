# 🏆 Design Philosophy

This document explains the core design philosophy and architectural principles of the DataAcquisition system.

## WAL-first Architecture

The core design principle is "data safety first". All collected data is immediately written to local Parquet files as write-ahead logs, then asynchronously written to InfluxDB. This ensures data is never lost, even in cases of network failures or storage service unavailability.

## Modular Design

The system adopts a clear layered architecture with interface abstractions, supporting flexible extension and replacement. New PLC protocols, storage backends, and data processing logic can be quickly integrated by implementing the corresponding interfaces.

## Operations-Friendly

Built-in comprehensive monitoring metrics and visualization interface, support for configuration hot updates, and detailed logging significantly reduce operational complexity.

For detailed design documentation, see: [Chinese Design Guide](design.md)
