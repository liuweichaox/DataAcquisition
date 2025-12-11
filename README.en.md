# 🛰️ DataAcquisition - Industrial PLC Data Acquisition System

[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%208.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](https://dotnet.microsoft.com/)

中文: [README.md](README.md)

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

### Overall Architecture Diagram

```
┌────────────────────────────┐        ┌────────────────────────────────┐
│          PLC 设备           │──────▶│        心跳监控层                │
│       PLC Device           │        │   Heartbeat Monitor            │
└────────────────────────────┘        └────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│        数据采集层           │
│   Data Collection Layer     │
│       (ChannelCollector)    │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│        数据处理层           │
│     Data Processing Layer   │
│       (DataProcessing)      │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│        队列服务层           │
│      Queue Service Layer    │
│         (LocalQueue)        │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│       存储层（双模式）       │
│     Storage Layer (Dual)    │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐        ┌────────────────────────────────┐
│        WAL 持久化           │──────▶│        时序数据库存储           │
│   WAL Persistence (Parquet) │        │  Time-Series DB Storage        │
│         (Parquet)           │        │          (InfluxDB)            │
└────────────────────────────┘        └────────────────────────────────┘
                 │                                 │
                 ▼                                 │ 写入失败 / Write Failed
┌────────────────────────────┐                     │
│         重试工作器          │◀────────────────────┘
│      Retry Worker          │
│      (RetryWorker)         │
└────────────────────────────┘
```

### Core Data Flow

1. **Acquisition Phase**: PLC → ChannelCollector → DataProcessingService
2. **Aggregation Phase**: LocalQueueService (aggregates by BatchSize)
3. **Persistence Phase**: Parquet WAL (immediate write) → InfluxDB (immediate write)
4. **Fault Tolerance Phase**: Delete WAL files on success, retry via RetryWorker on failure

## 📁 Project Structure

```
DataAcquisition/
├── DataAcquisition.Application/     # Application Layer - Interface Definitions
│   ├── Abstractions/               # Core Interface Abstractions
│   └── PlcRuntime.cs              # PLC Runtime Enums
├── DataAcquisition.Domain/         # Domain Layer - Core Models
│   ├── Models/                     # Data Models
│   └── OperationalEvents/          # Operational Events
├── DataAcquisition.Infrastructure/ # Infrastructure Layer - Implementations
│   ├── Clients/                    # PLC Client Implementations
│   ├── DataAcquisitions/           # Data Acquisition Services
│   ├── DataStorages/               # Data Storage Services
│   └── Metrics/                    # Metrics Collection
├── DataAcquisition.Gateway/        # Gateway Layer - Web API
│   ├── Configs/                    # Device Configuration Files
│   ├── Controllers/                # API Controllers
│   ├── Services/                   # Gateway Services
│   └── Views/                      # View Pages
└── DataAcquisition.sln             # Solution File
```

## 🚀 Quick Start

### Prerequisites

- .NET 10.0 or .NET 8.0 SDK (recommended to use the latest LTS version)
- InfluxDB 2.x (optional, for time-series data storage)
- Supported PLC devices (Modbus TCP, Beckhoff ADS, Inovance, Mitsubishi, Siemens)

> **Note**: The project supports multi-target frameworks (.NET 10.0, .NET 8.0). You can choose the appropriate version based on your deployment environment. Both versions are LTS (Long Term Support) versions, suitable for production use.
> 
> **Version Selection Recommendations**:
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
   Edit `DataAcquisition.Gateway/Configs/M01C123.json` file to configure your PLC device information.

4. **Run the System**

```bash
# Run with default framework
dotnet run --project DataAcquisition.Gateway

# Or run with specific framework
dotnet run -f net10.0 --project DataAcquisition.Gateway
dotnet run -f net8.0 --project DataAcquisition.Gateway
```

5. **Build for Specific Framework**

```bash
# Build for all target frameworks
dotnet build

# Build for specific framework
dotnet build -f net10.0
dotnet build -f net8.0
```

6. **Access Monitoring Interface**

- Metrics Visualization: http://localhost:8000/metrics
- Prometheus Metrics: http://localhost:8000/metrics/raw
- API Documentation: http://localhost:8000/swagger (if enabled)

## ⚙️ Configuration Guide

### Device Configuration Example

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
      "Measurement": "temperature",
      "ChannelCode": "PLC01C01",
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Conditional",
      "EnableBatchRead": true,
      "BatchReadRegister": "D200",
      "BatchReadLength": 20,
      "DataPoints": [
        {
          "FieldName": "temp_value",
          "Register": "D200",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value * 0.1"
        }
      ],
      "ConditionalAcquisition": {
        "Register": "D210",
        "DataType": "short",
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

### Application Configuration (appsettings.json)

```json
{
  "InfluxDb": {
    "Url": "http://localhost:8086",
    "Token": "your-token",
    "Org": "your-org",
    "Bucket": "your-bucket"
  },
  "Parquet": {
    "Directory": "./Data/parquet",
    "MaxFileSize": 104857600,
    "MaxFileAge": 86400
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## 🔌 API Usage Examples

### Real-time Data Subscription (SignalR)

```javascript
// Frontend JavaScript Example
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/dataHub")
  .build();

connection.on("DataReceived", (data) => {
  console.log("Data received:", data);
});

connection.start().then(() => {
  console.log("Connection established");
});
```

### Metrics Data Query

```bash
# Get Prometheus format metrics
curl http://localhost:8000/metrics/raw

# Get JSON format metrics
curl http://localhost:8000/api/metrics-data
```

### PLC Write Operation

```csharp
// C# Client Example
var request = new PlcWriteRequest
{
    DeviceCode = "PLC01",
    Register = "D300",
    Value = 100
};

var response = await httpClient.PostAsJsonAsync("/api/plc/write", request);
```

## 📊 Core Module Documentation

### PLC Client Implementations

| Protocol     | Implementation Class          | Description                         |
| ------------ | ----------------------------- | ----------------------------------- |
| Mitsubishi   | `MitsubishiPlcClientService`  | Mitsubishi PLC communication client |
| Inovance     | `InovancePlcClientService`    | Inovance PLC communication client   |
| Beckhoff ADS | `BeckhoffAdsPlcClientService` | Beckhoff ADS protocol client        |
| Siemens      | `SiemensPlcClientService`     | Siemens PLC communication client    |

### ChannelCollector - Channel Collector

```csharp
public class ChannelCollector : IChannelCollector
{
    public async Task StartCollectionAsync(CancellationToken cancellationToken)
    {
        // PLC connection health check
        await CheckPlcConnectionAsync();

        // Trigger condition evaluation
        var shouldCollect = await EvaluateTriggerConditionsAsync();

        if (shouldCollect)
        {
            // Execute data acquisition
            var data = await CollectDataAsync();
            await ProcessAndStoreDataAsync(data);
        }
    }
}
```

### InfluxDbDataStorageService - Data Storage Service

```csharp
public class InfluxDbDataStorageService : IDataStorageService
{
    public async Task SaveAsync(DataMessage dataMessage)
    {
        // Convert to InfluxDB data point
        var point = ConvertToDataPoint(dataMessage);

        // Write to InfluxDB
        try
        {
            await _writeApi.WritePointAsync(point);
            _metricsCollector.RecordWriteLatency(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _metricsCollector.RecordError("influx_write");
            throw;
        }
    }
}
```

### MetricsCollector - Metrics Collector

The system includes 9 core monitoring metrics:

- `data_acquisition_collection_latency_ms` - Collection latency
- `data_acquisition_collection_rate` - Collection frequency
- `data_acquisition_queue_depth` - Queue depth
- `data_acquisition_write_latency_ms` - Write latency
- `data_acquisition_errors_total` - Error count
- `data_acquisition_connection_status_changes_total` - Connection status changes
- `data_acquisition_connection_duration_seconds` - Connection duration
- `data_acquisition_batch_size` - Batch size statistics
- `data_acquisition_throughput` - System throughput

## 🔄 Data Processing Flow

### Normal Flow

1. **Data Acquisition**: ChannelCollector reads data from PLC
2. **Data Processing**: DataProcessingService performs data transformation and validation
3. **Queue Aggregation**: LocalQueueService aggregates data by BatchSize
4. **WAL Write**: Immediate write to Parquet files as write-ahead log
5. **Primary Storage Write**: Immediate write to InfluxDB
6. **WAL Cleanup**: Delete corresponding Parquet files on successful write

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
- **File Rotation**: Configure MaxFileSize and MaxFileAge to prevent oversized files
- **Retry Interval**: RetryWorker defaults to 5 seconds, adjustable based on network conditions

## ❓ Frequently Asked Questions (FAQ)

### Q: What if data is lost?

A: The system uses a WAL-first architecture where all data is first written to Parquet files, then to InfluxDB. WAL files are only deleted when both writes succeed, ensuring zero data loss.

### Q: How to add new PLC protocols?

A: Implement the `IPlcClientService` interface and register the new protocol support in `PlcClientFactory`.

### Q: Do I need to restart after configuration changes?

A: No. The system uses FileSystemWatcher to monitor configuration file changes and supports hot reload.

### Q: Where can I view monitoring metrics?

A: Visit http://localhost:8000/metrics for the visualization interface, or http://localhost:8000/metrics/raw for Prometheus format metrics.

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
- [Element Plus](https://element-plus.org/) - Vue 3 component library

---

**If you have questions or suggestions, please submit an [Issue](https://github.com/your-username/G-DataAcquisition/issues) or contribute code via Pull Request!**
