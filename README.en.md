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
- [⚙️ Configuration Guide](docs/configuration.en.md)
- [🔌 API Usage Examples](docs/api-usage.en.md)
- [📊 Core Module Documentation](docs/modules.en.md)
- [🔄 Data Processing Flow](docs/data-flow.en.md)
- [🎯 Performance Optimization Recommendations](docs/performance.en.md)
- [❓ Frequently Asked Questions (FAQ)](docs/faq.en.md)
- [🏆 Design Philosophy](docs/design.en.md)
- [🤝 Contributing Guidelines](#-contributing-guidelines)
- [📄 Open Source License](#-open-source-license)
- [🙏 Acknowledgments](#-acknowledgments)

## 📖 Project Overview

DataAcquisition is a high-performance, high-reliability industrial data acquisition system built on .NET, specifically designed for PLC (Programmable Logic Controller) data acquisition scenarios. The system supports .NET 10.0 and .NET 8.0 (both LTS versions), employs a WAL-first architecture to ensure zero data loss, supporting advanced features like multi-PLC parallel acquisition, conditional trigger acquisition, and batch reading.

### 🎯 Core Features

- ✅ **WAL-first Architecture** - Write-ahead logging guarantees data integrity
- ✅ **Multi-PLC Parallel Acquisition** - Supports multiple PLC protocols (Modbus, Beckhoff ADS, Inovance, Mitsubishi, Siemens)
- ✅ **Conditional Trigger Acquisition** - Intelligent acquisition modes including edge triggering, value change triggering
- ✅ **Batch Reading Optimization** - Reduces network round-trips, improves efficiency
- ✅ **Hot Configuration Reload** - JSON configuration + file monitoring, no restart required
- ✅ **Real-time Monitoring** - Prometheus metrics + Vue3 visualization interface
- ✅ **Dual Storage Strategy** - InfluxDB + Parquet local persistence
- ✅ **Automatic Retry Mechanism** - Automatic reconnection on network failures, data retransmission

## 🏗️ System Architecture

### Distributed Architecture Overview

The system adopts an **Edge-Central** distributed architecture, supporting centralized management of multiple workshops and nodes:

```
                    ┌─────────────────────────────────────────┐
                    │         Central Web (Vue3)              │
                    │    Visualization / Monitoring Panel     │
                    └───────────────┬─────────────────────────┘
                                    │ HTTP/API
                    ┌───────────────▼─────────────────────────┐
                    │         Central API                     │
                    │  • Edge Node Registration/Heartbeat     │
                    │  • Telemetry Data Ingestion             │
                    │  • Query & Management APIs              │
                    │  • Prometheus Metrics Aggregation       │
                    └───────┬──────────────────┬──────────────┘
                            │                  │
              ┌─────────────┘                  └─────────────┐
              │                                               │
    ┌─────────▼─────────┐                         ┌─────────▼─────────┐
    │   Edge Agent #1   │                         │   Edge Agent #N   │
    │   (Workshop Node 1)│                         │   (Workshop Node N)│
    └─────────┬─────────┘                         └─────────┬─────────┘
              │                                               │
              └───────────────────────────────────────────────┘
```

### Edge Agent Internal Architecture

Each Edge Agent internally adopts a layered architecture to ensure zero data loss:

```
┌─────────────────────┐
│    PLC Devices      │ (Modbus/ADS/Inovance/Mitsubishi/Siemens)
└──────────┬──────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│   Heartbeat Monitor Layer               │  ← Connection Status Monitoring
└──────────────────┬──────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────┐
│   Data Acquisition Layer                │
│   • ChannelCollector                    │  ← Conditional Trigger Acquisition
│   • Batch Reading Optimization          │
└──────────────────┬──────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────┐
│   Queue Service Layer                   │
│   • LocalQueueService                   │  ← Batch Aggregation (BatchSize)
└──────────────────┬──────────────────────┘
                   │
         ┌─────────┴─────────┐
         ▼                   ▼
┌─────────────────┐   ┌──────────────────────────────┐
│  Parquet WAL    │   │  InfluxDB Storage            │
│  (Local Persist)│   │  (Time-Series Database)      │
└────────┬────────┘   └──────────────────────────────┘
         │
         │ Write Failed
         ▼
┌─────────────────────────────────────────┐
│   Retry Worker                          │  ← Automatic Retry Mechanism
└─────────────────────────────────────────┘
```

### Core Data Flow

#### Edge Agent Internal Flow

1. **Acquisition Phase**: PLC → ChannelCollector (supports conditional triggers, batch reading)
2. **Aggregation Phase**: LocalQueueService (aggregates data by BatchSize)
3. **Persistence Phase**:
   - Parquet WAL (immediate local write, ensures zero loss)
   - InfluxDB (immediate write to time-series database)
4. **Fault Tolerance Phase**: Delete WAL files on success, retry via RetryWorker on failure
5. **Reporting Phase**: Report data to Central API (optional, for centralized management)

#### Edge-Central Interaction Flow

1. **Registration Phase**: Edge Agent registers with Central API on startup (EdgeId, AgentBaseUrl, Hostname)
2. **Heartbeat Phase**: Periodically sends heartbeat (default 10 seconds), includes backlog and error information
3. **Telemetry Phase**: Batch reports collected data to Central API (optional)
4. **Monitoring Phase**: Central Web queries edge node status and metrics through Central API

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

### Prerequisites

- .NET 10.0 or .NET 8.0 SDK (recommended to use the latest LTS version)
- Node.js (recommended 18+) + npm (for running the Central Web frontend)
- InfluxDB 2.x (optional, for time-series data storage)
- Supported PLC devices (Modbus TCP, Beckhoff ADS, Inovance, Mitsubishi, Siemens)

> **Note**: The project supports multi-target frameworks (.NET 10.0, .NET 8.0). You can choose the appropriate version based on your deployment environment. Both versions are LTS (Long Term Support) versions, suitable for production use.
>
> **Version Selection Recommendations**:
>
> - **.NET 10.0**: Latest LTS version, supported until 2028, recommended for new deployments
> - **.NET 8.0**: Stable LTS version, supported until 2026, recommended for production environments

### Installation Steps

1. **Clone the Repository**

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
```

2. **Restore Dependencies**

```bash
dotnet restore
```

3. **Configure Devices**
   Create/edit device config files under `src/DataAcquisition.Edge.Agent/Configs/` (the repo already includes `TEST_PLC.json`; you can add more `*.json` as needed).

4. **Run the System**

```bash
# Start central-side API (Central API, default http://localhost:8000)
dotnet run --project src/DataAcquisition.Central.Api

# Start acquisition backend (Edge Agent)
dotnet run --project src/DataAcquisition.Edge.Agent

# Start central frontend (Central Web, Vue CLI dev server, default http://localhost:3000)
cd src/DataAcquisition.Central.Web
npm install
npm run serve

# Optional: run with a specific framework
dotnet run -f net8.0 --project src/DataAcquisition.Edge.Agent
dotnet run -f net8.0 --project src/DataAcquisition.Central.Api
dotnet run -f net10.0 --project src/DataAcquisition.Edge.Agent
dotnet run -f net10.0 --project src/DataAcquisition.Central.Api
```

> Note: The repo is set up to build/run **net8.0 by default when only .NET 8 SDK is installed**. When it detects **SDK >= 10**, it automatically enables the additional `net10.0` target.
>
> Default ports:
>
> - Central API: `http://localhost:8000`
> - Central Web (Vue dev server): `http://localhost:3000` (proxy `/api` and `/metrics` to `http://localhost:8000` via `vue.config.js`)
> - Edge Agent: `http://localhost:8001`

5. **Build for Specific Framework**

```bash
# Build for all target frameworks
dotnet build

# Build for specific framework
dotnet build -f net10.0
dotnet build -f net8.0
```

6. **Access Monitoring Interface**

- Central Web (frontend UI): http://localhost:3000
- Prometheus Metrics: http://localhost:8000/metrics
- API Documentation: Swagger not configured (can be enabled in code)

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

## ⚙️ Configuration Guide

Detailed configuration guide: [Configuration Documentation](docs/configuration.en.md)

### Quick Reference

- **Device Configuration Files**: Located in `src/DataAcquisition.Edge.Agent/Configs/` directory, format is `*.json`
- **Edge Agent Configuration**: Edit `src/DataAcquisition.Edge.Agent/appsettings.json`
- **Hot Reload**: Supports configuration file hot updates without service restart

Basic configuration example:

```json
{
  "IsEnabled": true,
  "PLCCode": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "Channels": [...]
}
```

## 🔌 API Usage Examples

Detailed API usage examples: [API Usage Documentation](docs/api-usage.en.md)

## 📊 Core Module Documentation

Detailed module documentation: [Core Module Documentation](docs/modules.en.md)

## 🔄 Data Processing Flow

Detailed data processing flow: [Data Processing Flow Documentation](docs/data-flow.en.md)

## 🎯 Performance Optimization Recommendations

Detailed performance optimization recommendations: [Performance Optimization Documentation](docs/performance.en.md)

## ❓ Frequently Asked Questions (FAQ)

Frequently asked questions: [FAQ Documentation](docs/faq.en.md)

## 🏆 Design Philosophy

Detailed design philosophy: [Design Philosophy Documentation](docs/design.en.md)

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
