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
- [⚙️ Configuration Guide](#-configuration-guide)
- [🔌 API Usage Examples](#-api-usage-examples)
- [📊 Core Module Documentation](#-core-module-documentation)
- [🔄 Data Processing Flow](#-data-processing-flow)
- [🎯 Performance Optimization Recommendations](#-performance-optimization-recommendations)
- [❓ Frequently Asked Questions (FAQ)](#-frequently-asked-questions-faq)
- [🏆 Design Philosophy](#-design-philosophy)
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

System configuration consists of two parts: device configuration and Edge Agent application configuration. For detailed configuration instructions, please refer to the following documentation:

### Configuration Documentation

- 📖 [Device Configuration Guide](docs/configuration/device-config.en.md) - Detailed instructions for device configuration files (PLC configs)
- ⚙️ [Edge Agent Application Configuration](docs/configuration/edge-agent-config.en.md) - Instructions for Edge Agent appsettings.json configuration
- 📊 [Configuration to Database Mapping](docs/configuration/database-mapping.en.md) - How configuration files map to InfluxDB database

### Quick Configuration

1. **Device Configuration**: Create `*.json` format device configuration files in the `src/DataAcquisition.Edge.Agent/Configs/` directory
2. **Application Configuration**: Edit `src/DataAcquisition.Edge.Agent/appsettings.json` to configure InfluxDB, Parquet, and other parameters

> **Tip**: The system supports hot configuration reload. No service restart is required after modifying configuration files.

## 🔌 API Usage Examples

### Metrics Data Query

```bash
# Get Prometheus format metrics
curl http://localhost:8000/metrics

# Get JSON format metrics
curl http://localhost:8000/api/metrics-data

# Get metrics information
curl http://localhost:8000/api/metrics-data/info
```

### PLC Connection Status Query

```bash
# Get PLC connection status
curl http://localhost:8000/api/DataAcquisition/GetPLCConnectionStatus
```

### PLC Write Operation

```csharp
// C# Client Example
var request = new PLCWriteRequest
{
    PLCCode = "M01C123",
    Items = new List<PLCWriteItem>
    {
        new PLCWriteItem
        {
            Address = "D300",
            DataType = "short",
            Value = 100
        }
    }
};

var response = await httpClient.PostAsJsonAsync("/api/DataAcquisition/WriteRegister", request);
```

## 📊 Core Module Documentation

### PLC Client Implementations

| Protocol     | Implementation Class          | Description                         |
| ------------ | ----------------------------- | ----------------------------------- |
| Mitsubishi   | `MitsubishiPLCClientService`  | Mitsubishi PLC communication client |
| Inovance     | `InovancePLCClientService`    | Inovance PLC communication client   |
| Beckhoff ADS | `BeckhoffAdsPLCClientService` | Beckhoff ADS protocol client        |
| Siemens      | `SiemensPLCClientService`     | Siemens PLC communication client    |

### ChannelCollector - Channel Collector

```csharp
public class ChannelCollector : IChannelCollector
{
    public async Task CollectAsync(DeviceConfig config, DataAcquisitionChannel channel,
        IPLCClientService client, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            // Check PLC connection status
            if (!await WaitForConnectionAsync(config, ct))
                continue;

            // Acquire device lock for thread-safe PLC access
            if (!_plcLifecycle.TryGetLock(config.PLCCode, out var locker))
                continue;

            await locker.WaitAsync(ct);
            try
            {
                var timestamp = DateTime.Now;

                // Handle different acquisition modes
                if (channel.AcquisitionMode == AcquisitionMode.Always)
                {
                    await HandleUnconditionalCollectionAsync(config, channel, client, timestamp, ct);
                }
                else if (channel.AcquisitionMode == AcquisitionMode.Conditional)
                {
                    await HandleConditionalCollectionAsync(config, channel, client, timestamp, ct);
                }
            }
            finally
            {
                locker.Release();
            }
        }
    }
}
```

### InfluxDbDataStorageService - Data Storage Service

```csharp
public class InfluxDbDataStorageService : IDataStorageService
{
    public async Task<bool> SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return true;

        _writeStopwatch.Restart();
        var writeSuccess = false;
        Exception? writeException = null;
        var resetEvent = new System.Threading.ManualResetEventSlim(false);

        try
        {
            // Convert batch of messages to points
            var points = dataMessages.Select(ConvertToPoint).ToList();
            using var writeApi = _client.GetWriteApi();

            // Set up error handler callback to catch write failures
            writeApi.EventHandler += (sender, args) =>
            {
                writeException = new Exception($"InfluxDB write failed: {args.GetType().Name} - {args}");
                writeSuccess = false;
                resetEvent.Set();
                _logger.LogError(writeException, "[ERROR] InfluxDB write error event triggered: {EventType} - {Message}",
                    args.GetType().Name, writeException.Message);
            };

            writeApi.WritePoints(_bucket, _org, points);
            writeApi.Flush();

            // Wait long enough to detect errors (InfluxDB writes asynchronously, errors may be delayed)
            _logger.LogDebug("Waiting for InfluxDB batch write response, max wait 5 seconds...");
            var errorOccurred = resetEvent.Wait(TimeSpan.FromSeconds(5));

            if (errorOccurred)
            {
                _logger.LogWarning("InfluxDB batch write error event triggered");
            }
            else
            {
                writeSuccess = true;
                _logger.LogDebug("No error detected within 5 seconds, assuming write success");
            }

            _writeStopwatch.Stop();

            if (!writeSuccess)
            {
                throw writeException ?? new Exception("InfluxDB write failed");
            }

            // Record batch efficiency metrics and write latency
            var batchSize = dataMessages.Count;
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            _metricsCollector?.RecordBatchWriteEfficiency(batchSize, _writeStopwatch.ElapsedMilliseconds);
            _metricsCollector?.RecordWriteLatency(measurement, _writeStopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            // Handle batch write errors
            var plcCode = dataMessages.FirstOrDefault()?.PLCCode ?? "unknown";
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            var channelCode = dataMessages.FirstOrDefault()?.ChannelCode;
            _metricsCollector?.RecordError(plcCode, measurement, channelCode);
            _logger.LogError(ex, "[ERROR] Time-series database batch insert failed: {Message}", ex.Message);
            return false;
        }
        finally
        {
            resetEvent.Dispose();
        }
    }
}
```

### MetricsCollector - Metrics Collector

The system includes the following core monitoring metrics:

#### Acquisition Metrics

- **`data_acquisition_collection_latency_ms`** - Collection latency (time from PLC read to database write, milliseconds)
- **`data_acquisition_collection_rate`** - Collection rate (data points per second, points/s)

#### Queue Metrics

- **`data_acquisition_queue_depth`** - Queue depth (Channel pending + batch accumulated total pending messages)
- **`data_acquisition_processing_latency_ms`** - Processing latency (queue processing delay, milliseconds)

#### Storage Metrics

- **`data_acquisition_write_latency_ms`** - Write latency (database write delay, milliseconds)
- **`data_acquisition_batch_write_efficiency`** - Batch write efficiency (batch size / write time, points/ms)

#### Error & Connection Metrics

- **`data_acquisition_errors_total`** - Total errors (by device/channel)
- **`data_acquisition_connection_status_changes_total`** - Connection status change count
- **`data_acquisition_connection_duration_seconds`** - Connection duration (seconds)

## 🔄 Data Processing Flow

### Normal Flow

1. **Data Acquisition**: ChannelCollector reads data from PLC
2. **Queue Aggregation**: LocalQueueService aggregates data by BatchSize
3. **WAL Write**: Immediate write to Parquet files as write-ahead log
4. **Primary Storage Write**: Immediate write to InfluxDB
5. **WAL Cleanup**: Delete corresponding Parquet files on successful write

### Exception Handling Flow

1. **Network Exception**: Automatic reconnection mechanism, heartbeat monitoring ensures connection status
2. **Storage Failure**: WAL files retained, periodically retried by ParquetRetryWorker
3. **Configuration Error**: Configuration validation and hot reload mechanism

## 🎯 Performance Optimization Recommendations

### Acquisition Parameter Tuning

| Parameter           | Recommended Value | Description                     |
| ------------------- | ----------------- | ------------------------------- |
| BatchSize           | 10-50             | Balance latency and throughput  |
| AcquisitionInterval | 100-500ms         | Adjust based on PLC performance |
| HeartbeatInterval   | 5000ms            | Connection monitoring frequency |

### Storage Optimization

- **Parquet Compression**: Use Snappy compression to reduce disk usage
- **Retry Interval**: RetryWorker defaults to 5 seconds, adjustable based on network conditions

## ❓ Frequently Asked Questions (FAQ)

### Q: What if data is lost?

A: The system uses a WAL-first architecture where all data is first written to Parquet files, then to InfluxDB. WAL files are only deleted when both writes succeed, ensuring zero data loss.

### Q: How to add new PLC protocols?

A: Implement the `IPLCClientService` interface and register the new protocol support in `PLCClientFactory`.

### Q: Do I need to restart after configuration changes?

A: No. The system uses FileSystemWatcher to monitor configuration file changes and supports hot reload.

### Q: Where can I view monitoring metrics?

A: Visit http://localhost:8000/metrics for the visualization interface or Prometheus raw format metrics, or http://localhost:8000/api/metrics-data to get JSON format metrics data (recommended).

### Q: How to extend storage backends?

A: Implement the `IDataStorageService` interface while maintaining consistency with the queue service write contract.

## 🏆 Design Philosophy

### WAL-first Architecture

The core design philosophy is "data safety first." All acquired data is immediately written to local Parquet files as write-ahead logs before being asynchronously written to InfluxDB. This design ensures data integrity even during network failures, storage service unavailability, and other exceptional conditions.

### Modular Design

The system employs a clear layered architecture with interface abstractions for each module, supporting flexible extension and replacement. New PLC protocols, storage backends, and data processing logic can be quickly integrated by implementing the corresponding interfaces.

### Operations-Friendly

Built-in comprehensive monitoring metrics and visualization interfaces, support for hot configuration updates, and detailed logging significantly reduce operational complexity.

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
