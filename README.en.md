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
- Prometheus Metrics: http://localhost:8000/metrics
- API Documentation: Swagger not configured (can be enabled in code)

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

### 📊 Configuration to Database Mapping

The system maps configuration files to InfluxDB time-series database. Here's the mapping relationship:

#### Mapping Table

| Configuration Field                 | InfluxDB Structure      | Description                                | Example                      |
| ----------------------------------- | ----------------------- | ------------------------------------------ | ---------------------------- |
| `Channels[].Measurement`            | **Measurement**         | Measurement name (table name)              | `"sensor"`                   |
| `PLCCode`                           | **Tag**: `plc_code`     | PLC device code tag                        | `"M01C123"`                  |
| `Channels[].ChannelCode`            | **Tag**: `channel_code` | Channel code tag                           | `"M01C01"`                   |
| `EventType`                         | **Tag**: `event_type`   | Event type tag (Start/End/Data)            | `"Start"`, `"End"`, `"Data"` |
| `Channels[].DataPoints[].FieldName` | **Field**               | Data field name                            | `"up_temp"`, `"down_temp"`   |
| `CycleId`                           | **Field**: `cycle_id`   | Acquisition cycle unique identifier (GUID) | `"guid-xxx"`                 |
| Acquisition time                    | **Timestamp**           | Data point timestamp                       | `2025-01-15T10:30:00Z`       |

#### Configuration Example and Line Protocol

**Configuration File** (`M01C123.json`):

```json
{
  "PLCCode": "M01C123",
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "M01C01",
      "DataPoints": [
        {
          "FieldName": "up_temp",
          "Register": "D6002",
          "Index": 2,
          "DataType": "short"
        },
        {
          "FieldName": "down_temp",
          "Register": "D6004",
          "Index": 4,
          "DataType": "short",
          "EvalExpression": "value / 1000.0"
        }
      ],
      "ConditionalAcquisition": {
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

**Generated InfluxDB Line Protocol**:

**Start Event** (conditional acquisition start):

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Start up_temp=250i,down_temp=0.18,cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312200000000000
```

**Data Event** (normal data point):

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Data up_temp=255i,down_temp=0.19 1705312210000000000
```

**End Event** (conditional acquisition end):

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=End cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312300000000000
```

#### Line Protocol Format Explanation

InfluxDB Line Protocol format:

```
measurement,tag1=value1,tag2=value2 field1=value1,field2=value2 timestamp
```

**Field Type Explanation**:

- **Measurement**: From configuration `Measurement`, e.g., `"sensor"`
- **Tags** (for filtering and grouping, indexed fields):
  - `plc_code`: PLC device code
  - `channel_code`: Channel code
  - `event_type`: Event type (`Start`/`End`/`Data`)
- **Fields** (actual data values):
  - All fields from `DataPoints[].FieldName` (e.g., `up_temp`, `down_temp`)
  - `cycle_id`: Conditional acquisition cycle ID (GUID, used to link Start/End events)
  - Numeric types: integers use `i` suffix (e.g., `250i`), floats are written directly (e.g., `0.18`)
- **Timestamp**: Data acquisition time (nanosecond precision)

#### Query Examples

**Query data from a specific PLC channel within a specified time range (1h)**:

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["plc_code"] == "M01C123")
  |> filter(fn: (r) => r["channel_code"] == "M01C01")
```

**Query a complete conditional acquisition cycle**:

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["cycle_id"] == "550e8400-e29b-41d4-a716-446655440000")
```

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
curl http://localhost:8000/api/DataAcquisition/GetPlcConnectionStatus
```

### PLC Write Operation

```csharp
// C# Client Example
var request = new PlcWriteRequest
{
    PlcCode = "M01C123",
    Items = new List<PlcWriteItem>
    {
        new PlcWriteItem
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
| Mitsubishi   | `MitsubishiPlcClientService`  | Mitsubishi PLC communication client |
| Inovance     | `InovancePlcClientService`    | Inovance PLC communication client   |
| Beckhoff ADS | `BeckhoffAdsPlcClientService` | Beckhoff ADS protocol client        |
| Siemens      | `SiemensPlcClientService`     | Siemens PLC communication client    |

### ChannelCollector - Channel Collector

```csharp
public class ChannelCollector : IChannelCollector
{
    public async Task CollectAsync(DeviceConfig config, DataAcquisitionChannel channel,
        IPlcClientService client, CancellationToken ct = default)
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
    public async Task SaveAsync(DataMessage dataMessage)
    {
        _writeStopwatch.Restart();
        try
        {
            // Convert data message to InfluxDB point
            var point = ConvertToPoint(dataMessage);

            // Write to InfluxDB using WriteApi
            using var writeApi = _client.GetWriteApi();
            writeApi.WritePoint(_bucket, _org, point);

            // Record metrics for monitoring
            _writeStopwatch.Stop();
            _metricsCollector?.RecordWriteLatency(dataMessage.Measurement, _writeStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Record error metrics and log exception
            _metricsCollector?.RecordError(dataMessage.PLCCode ?? "unknown",
                dataMessage.Measurement, dataMessage.ChannelCode);
            _logger.LogError(ex, "[ERROR] InfluxDB write failed: {Message}", ex.Message);
        }
    }

    public async Task SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return;

        _writeStopwatch.Restart();
        try
        {
            // Convert batch of messages to points
            var points = dataMessages.Select(ConvertToPoint).ToList();

            // Batch write to InfluxDB
            using var writeApi = _client.GetWriteApi();
            writeApi.WritePoints(_bucket, _org, points);

            // Record batch efficiency metrics
            _writeStopwatch.Stop();
            var batchSize = dataMessages.Count;
            _metricsCollector?.RecordBatchWriteEfficiency(batchSize, _writeStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Handle batch write errors
            var plcCode = dataMessages.FirstOrDefault()?.PLCCode ?? "unknown";
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            _metricsCollector?.RecordError(plcCode, measurement, null);
            _logger.LogError(ex, "[ERROR] InfluxDB batch write failed: {Message}", ex.Message);
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
- [Element Plus](https://element-plus.org/) - Vue 3 component library

---

**If you have questions or suggestions, please submit an [Issue](https://github.com/your-username/G-DataAcquisition/issues) or contribute code via Pull Request!**
