# 🏆 Design Philosophy

This document explains the core design philosophy and architectural principles of the DataAcquisition system.

## Overview

This page focuses on design principles and architectural decisions. Use the index for full navigation.

## Table of Contents

- [WAL-first Architecture](#wal-first-architecture)
- [Modular Design](#modular-design)
- [Operations-Friendly](#operations-friendly)
- [Edge-Central Distributed Architecture](#edge-central-distributed-architecture)
- [Security](#security)

## WAL-first Architecture

The core design principle is "data safety first". All collected data is immediately written to local Parquet files as write-ahead logs, then asynchronously written to InfluxDB. This ensures data is never lost, even in cases of network failures or storage service unavailability.

### Core Principles

- **Data First**: Data safety is the highest priority
- **Local Persistence**: All data must be written to local storage first
- **Asynchronous Writes**: Primary storage writes use asynchronous methods, not blocking acquisition
- **Automatic Recovery**: System automatically retries failed write operations

### Implementation Approach

1. **Dual Write Strategy**: Data is written to both Parquet WAL and InfluxDB simultaneously
2. **Atomic Operations**: WAL files are only deleted when both writes succeed
3. **Retry Mechanism**: WAL files are retained on write failures, with periodic retries
4. **Zero Loss Guarantee**: Ensures data is never lost under any circumstances

## Modular Design

The system adopts a clear layered architecture with interface abstractions, supporting flexible extension and replacement. New PLC protocols, storage backends, and data processing logic can be quickly integrated by implementing the corresponding interfaces.

### Layered Architecture

```
┌─────────────────────────────────────┐
│     Application Layer               │  ← Interface definitions
├─────────────────────────────────────┤
│     Domain Layer                    │  ← Domain models
├─────────────────────────────────────┤
│     Infrastructure Layer            │  ← Concrete implementations
└─────────────────────────────────────┘
```

### Interface Abstractions

- **IPlcClientService**: PLC client service interface
- **IDataStorageService**: Data storage service interface (TSDB, e.g. InfluxDB)
- **IWalStorageService**: WAL storage service interface (e.g. Parquet)
- **IChannelCollector**: Channel collector interface
- **IQueueService**: Queue service interface
- **IMetricsCollector**: Metrics collector interface

### Extensibility

- **Plugin-based**: New features can be quickly integrated by implementing interfaces
- **Replaceable**: Core components can be replaced with other implementations
- **Loosely Coupled**: Modules interact through interfaces, reducing coupling

## Operations-Friendly

Built-in comprehensive monitoring metrics and visualization interface, support for configuration hot updates, and detailed logging significantly reduce operational complexity.

### Monitoring Capabilities

- **Prometheus Metrics**: Complete system metrics exposure
- **Visualization Interface**: Vue3 frontend interface for intuitive system status display
- **Logging System**: SQLite log database supporting query and analysis
- **Health Checks**: Built-in health check endpoints

### Maintainability

- **Configuration Hot Updates**: Apply new configurations without restart
- **Detailed Logging**: Record key operations and error information
- **Error Tracking**: Complete error stack traces and context information
- **Comprehensive Documentation**: Detailed configuration and usage documentation

### Easy Deployment

- **Containerization Support**: Supports Docker deployment
- **Environment Variables**: Supports configuration via environment variables
- **Multi-platform**: Supports Windows, Linux, macOS
- **Clear Dependencies**: Clear dependency relationships and requirements

## Edge-Central Distributed Architecture

The system adopts an Edge-Central distributed architecture, supporting centralized management of multiple workshops and nodes.

### Architectural Advantages

- **Distributed Deployment**: Edge Agents can be distributed across multiple workshops
- **Centralized Management**: Central API provides unified management and monitoring of all nodes
- **High Availability**: Failure of a single node does not affect other nodes
- **Scalability**: Easy to add new Edge Agent nodes

### Data Flow

1. **Local Acquisition**: Edge Agent acquires PLC data locally
2. **Local Storage**: Data is stored locally first (Parquet WAL)
3. **Remote Reporting**: Optionally report data to Central API
4. **Centralized Monitoring**: Central Web provides unified monitoring of all nodes

> For detailed performance optimization recommendations and best practices, please refer to [Performance Optimization Guide](performance.en.md)

## Security

The system adopts the following security measures:

- **InfluxDB Token Authentication**: The system authenticates with InfluxDB through Token, configured in `appsettings.json`. It is recommended to use environment variables to manage sensitive information
- **CORS Configuration**: Supports configuration of allowed frontend origins to prevent cross-origin attacks
- **Environment Variable Configuration**: Supports managing sensitive configuration information (such as Token) through environment variables to avoid storing in plain text in configuration files

**Security Recommendations**:
- Production environments should use environment variables to manage sensitive information such as InfluxDB Token
- It is recommended to configure firewall rules at the network level to restrict API access
- It is recommended to use a reverse proxy (such as Nginx) to configure HTTPS in production environments

## Next Steps

After understanding the design philosophy, continue with:

- [Documentation Index](index.en.md)
- [Data Flow](data-flow.en.md)
- [Core Modules](modules.en.md)
