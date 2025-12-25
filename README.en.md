# 🛰️ DataAcquisition - Industrial PLC Data Acquisition System

[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%208.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Version](https://img.shields.io/badge/version-1.0.0-blue)]()
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)]()

中文: [README.md](README.md)

## 📋 Table of Contents

- [📖 Project Overview](#-project-overview)
- [🎯 Core Features](#-core-features)
- [🏗️ System Architecture](#-system-architecture)
- [📁 Project Structure](#-project-structure)
- [🚀 Quick Start](#-quick-start)
- [📚 Documentation Navigation](#-documentation-navigation)
- [🤝 Contributing Guidelines](#-contributing-guidelines)
- [📄 Open Source License](#-open-source-license)
- [🙏 Acknowledgments](#-acknowledgments)

## 📖 Project Overview

DataAcquisition is an industrial-grade PLC data acquisition system built on .NET. The system employs a **WAL-first (Write-Ahead Logging) architecture** to ensure zero data loss, supports **Edge-Central distributed architecture** for centralized management across multiple workshops. It provides advanced features like multi-PLC parallel acquisition, conditional trigger acquisition, and batch reading optimization, supports configuration hot updates and real-time monitoring, ready to use out of the box, operations-friendly.

**Tech Stack:**
- Runtime: .NET 10.0 / .NET 8.0 (LTS versions)
- Data Storage: InfluxDB 2.x (time-series database) + Parquet (local WAL)
- Monitoring: Prometheus metrics + Vue3 visualization interface
- Architecture: Edge-Central distributed architecture

### 🎯 Core Features

| Feature | Description |
|---------|-------------|
| 🔒 **Data Safety** | WAL-first architecture, all data written to local Parquet files first, ensuring zero loss |
| 🔀 **Multi-Protocol Support** | Supports PLC protocols: Mitsubishi, Inovance, BeckhoffAds |
| ⚡ **High Performance** | Multi-PLC parallel acquisition, batch reading optimization, reduces network round-trips |
| 🎯 **Intelligent Acquisition** | Supports conditional trigger acquisition (edge trigger, value change trigger) and continuous acquisition modes |
| 🔄 **Hot Configuration** | JSON configuration files + file system monitoring, configuration changes without service restart |
| 📊 **Real-time Monitoring** | Prometheus metrics exposure, Vue3 visualization interface, real-time system status |
| 💾 **Dual Storage** | InfluxDB time-series database + Parquet local persistence (WAL) |
| 🔁 **Automatic Fault Tolerance** | Automatic reconnection on network failures, automatic retry on write failures, ensures data integrity |

## 🏗️ System Architecture

### Distributed Architecture Overview

The system adopts an **Edge-Central distributed architecture**, supporting centralized management and monitoring of multiple workshops and nodes:

```
                    ┌─────────────────────────────────────────┐
                    │           Central Web (Vue3)            │
                    │     Visualization / Monitoring Panel    │
                    └───────────────────┬─────────────────────┘
                                        │ HTTP/API
                    ┌───────────────────▼─────────────────────┐
                    │         Central API                     │
                    │  • Edge Node Registration/Heartbeat     │
                    │  • Telemetry Data Ingestion             │
                    │  • Query & Management APIs              │
                    │  • Prometheus Metrics Aggregation       │
                    └───────┬─────────────────────┬───────────┘
                            │                     │
              ┌─────────────┘                     └───────────┐
              │                                               │
    ┌─────────▼─────────┐                          ┌──────────▼────────┐
    │   Edge Agent #1   │                          │   Edge Agent #N   │
    │    ( Node 1)      │                          │     ( Node N)     │
    └─────────┬─────────┘                          └─────────┬─────────┘
              │                                              │
              └──────────────────────────────────────────────┘
```

### Edge Agent Internal Architecture

Each Edge Agent adopts a layered architecture design with clear responsibilities at each layer to ensure zero data loss:

```
┌────────────────────────────┐        ┌──────────────────────────┐
│        PLC Device          │──────▶ │  Heartbeat Monitor Layer │
└────────────────────────────┘        └──────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│   Data Acquisition Layer   │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│    Queue Service Layer     │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│          Storage Layer     │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐        ┌──────────────────────────────┐
│      WAL Persistence       │──────▶ │ Time-Series Database Storage │
└────────────────────────────┘        └──────────────────────────────┘
                 │                                 │
                 ▼                                 │  Write Failed
┌────────────────────────────┐                     │
│      Retry Worker          │◀────────────────────┘
└────────────────────────────┘
```

### Core Data Flow

#### Edge Agent Internal Flow

1. **Data Acquisition Phase**: PLC devices → `ChannelCollector` (supports conditional triggers, batch reading optimization)
2. **Data Aggregation Phase**: `LocalQueueService` aggregates data by configured `BatchSize`
3. **Data Persistence Phase**:
   - **Parquet WAL**: Immediate write to local Parquet files (write-ahead logging, ensures zero loss)
   - **InfluxDB**: Synchronous write to time-series database (primary storage)
4. **Fault Tolerance Phase**: Delete WAL files on successful write; retain WAL files on failure for periodic retry by `RetryWorker`
5. **Data Reporting Phase**: Optionally report data to Central API (for centralized management and monitoring)

#### Edge-Central Interaction Flow

1. **Node Registration Phase**: Edge Agent automatically registers with Central API on startup (EdgeId, AgentBaseUrl, Hostname)
2. **Heartbeat Reporting Phase**: Periodically sends heartbeat information (default 10 seconds interval), includes queue backlog, error information, and other status
3. **Telemetry Data Reporting Phase**: Batch reports collected time-series data to Central API (optional feature)
4. **Monitoring Query Phase**: Central Web frontend queries edge node status, metrics, and logs through Central API

## 📁 Project Structure

```
DataAcquisition/
├── src/DataAcquisition.Application/     # Application Layer - Interface Definitions
│   ├── Abstractions/               # Core Interface Abstractions
│   └── PLCRuntime.cs              # PLC Runtime Enums
├── src/DataAcquisition.Contracts/       # Contracts Layer - External DTOs/Protocols
├── src/DataAcquisition.Domain/         # Domain Layer - Core Models
│   ├── Models/                     # Data Models
│   └── OperationalEvents/          # Operational Events
├── src/DataAcquisition.Infrastructure/ # Infrastructure Layer - Implementations
│   ├── Clients/                    # PLC Client Implementations
│   ├── DataAcquisitions/           # Data Acquisition Services
│   ├── DataStorages/               # Data Storage Services
│   └── Metrics/                    # Metrics Collection
├── src/DataAcquisition.Edge.Agent/ # Edge Agent - workshop acquisition + metrics + local APIs
│   ├── Configs/                    # Device configuration files
│   └── Controllers/                # Management API controllers
├── src/DataAcquisition.Central.Api/ # Central API - central-side APIs (edge register/heartbeat/ingest, query & admin)
├── src/DataAcquisition.Central.Web/ # Central Web - pure frontend (Vue CLI / Vue3), talks to Central API via /api
├── src/DataAcquisition.Simulator/      # PLC Simulator - For Testing
│   ├── Simulator.cs               # Simulator Core Logic
│   ├── Program.cs                 # Program Entry Point
│   └── README.md                  # Simulator Documentation
└── DataAcquisition.sln             # Solution File
```

## 🚀 Quick Start

Want to get started quickly? Check out the [Getting Started Guide](docs/getting-started.en.md), which provides complete steps from scratch, including:

- Prerequisites and installation steps
- InfluxDB configuration instructions
- Device configuration file creation
- System startup and verification
- Testing with PLC simulator

> **Tip**: If this is your first time using the system, we recommend following the steps in the [Getting Started Guide](docs/getting-started.en.md). If you're already familiar with the system, you can directly check the [Configuration Guide](docs/configuration.en.md) and [API Usage Examples](docs/api-usage.en.md).

### 🧪 Testing with PLC Simulator

The project includes a standalone PLC simulator (`DataAcquisition.Simulator`) that simulates Mitsubishi PLC behavior for testing data acquisition functionality without requiring actual PLC hardware.

#### Start the Simulator

```bash
cd src/DataAcquisition.Simulator
dotnet run
```

#### Simulator Features

- ✅ Simulates Mitsubishi PLC (MelsecA1EServer)
- ✅ Auto-updates heartbeat register (D100)
- ✅ Simulates 7 sensor metrics (temperature, pressure, current, voltage, light barrier position, servo speed, production serial number)
- ✅ Supports conditional acquisition testing (production serial trigger)
- ✅ Interactive command control (set/get/info/exit)
- ✅ Real-time data display

#### Quick Test Flow

1. **Start the Simulator**:

```bash
cd src/DataAcquisition.Simulator
dotnet run
```

2. **Configure Test Device**:

   Create `TEST_PLC.json` in `src/DataAcquisition.Edge.Agent/Configs/` directory (refer to the complete configuration example in `src/DataAcquisition.Simulator/README.md`)

3. **Start the Acquisition System**:

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
dotnet run --project src/DataAcquisition.Central.Api

cd src/DataAcquisition.Central.Web
npm install
npm run serve
```

4. **Observe Data Acquisition**:
   - Visit http://localhost:3000 for the central UI (Edges/Metrics/Logs)
   - Visit http://localhost:8000/metrics for Central API's own metrics page
   - Check the `sensor` and `production` measurements in InfluxDB

For detailed information, please refer to: [src/DataAcquisition.Simulator/README.md](src/DataAcquisition.Simulator/README.md)

## 📚 Documentation Navigation

Choose the appropriate documentation reading path based on your use case:

### New User Getting Started

If this is your first time using the system, we recommend reading in the following order:

1. **[Getting Started Guide](docs/getting-started.en.md)** - Get started from scratch, quickly get up and running
   - Prerequisites and installation steps
   - System configuration and startup
   - Testing with PLC simulator

2. **[Configuration Guide](docs/configuration.en.md)** - Learn how to configure the system
   - Device configuration file details
   - Application configuration instructions
   - Configuration examples and use cases

3. **[FAQ](docs/faq.en.md)** - Reference when encountering issues
   - Common questions and answers
   - Troubleshooting guide
   - Configuration verification methods

### Daily Use

If you're already familiar with the system and need daily use and maintenance:

- **[API Usage Examples](docs/api-usage.en.md)** - Query data and manage the system
  - Metrics data query
  - PLC connection status query
  - Log query and management

- **[Performance Optimization Recommendations](docs/performance.en.md)** - Optimize system performance
  - Acquisition parameter tuning
  - Storage optimization strategies
  - System resource optimization

### Deep Dive

If you want to understand the system architecture and implementation in depth:

- **[Core Module Documentation](docs/modules.en.md)** - Understand system core modules
  - PLC client implementation
  - Channel collector
  - Data storage service

- **[Data Processing Flow](docs/data-flow.en.md)** - Understand data flow process
   - Normal processing flow
   - Exception handling mechanism
   - Data consistency guarantees

- **[Design Philosophy](docs/design.en.md)** - Understand system design philosophy
   - WAL-first architecture
   - Modular design
   - Distributed architecture

## ⚙️ Configuration Guide

Detailed configuration guide: [Configuration Documentation](docs/configuration.en.md)

### Quick Reference

| Configuration Type | Location | Description |
|-------------------|----------|-------------|
| Device Configuration | `src/DataAcquisition.Edge.Agent/Configs/*.json` | One JSON configuration file per PLC device |
| Edge Agent Configuration | `src/DataAcquisition.Edge.Agent/appsettings.json` | Application layer configuration (database, API, etc.) |
| Hot Configuration Reload | Auto-detected | Supports automatic hot reload on configuration file changes, no service restart required |

**Device Configuration Example:**

```json
{
  "IsEnabled": true,
  "PLCCode": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "PLC01C01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 10,
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Always",
      "DataPoints": [
        {
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        }
      ]
    }
  ]
}
```


## 🤝 Contributing Guidelines

We welcome contributions of all kinds! Please follow these steps:

1. Fork the project
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Development Environment Setup

```bash
# Clone the project
git clone https://github.com/liuweichaox/DataAcquisition.git

# Install dependencies
dotnet restore

# Run tests
dotnet test

# Build the project
dotnet build
```

### Code Standards

- Follow .NET coding conventions
- Use meaningful naming
- Add necessary XML documentation
- Write unit tests

## 📄 Open Source License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

Thanks to the following open-source projects:

- [.NET](https://dotnet.microsoft.com/) - Powerful development platform
- [InfluxDB](https://www.influxdata.com/) - High-performance time-series database
- [Prometheus](https://prometheus.io/) - Monitoring system
- [Vue.js](https://vuejs.org/) - Progressive JavaScript framework

---

**If you have questions or suggestions, please submit an [Issue](https://github.com/liuweichaox/DataAcquisition/issues) or contribute code via Pull Request!**
