# 🛰️ PLC Data Acquisition System

[![Stars](https://img.shields.io/github/stars/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/stargazers)
[![Forks](https://img.shields.io/github/forks/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/network/members)
[![License](https://img.shields.io/github/license/liuweichaox/DataAcquisition.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](#)

**[中文](README.md) | English**

## 📙 Overview

This system collects real-time data from PLCs and pushes results to **message queues** and **time-series databases** to support **online monitoring, performance analytics, and fault diagnostics**. It's built on .NET 8.0 and runs on Windows, Linux, and macOS.

## 💡 Key Features

- **Reliable I/O**: Modbus TCP (sample) with extensible protocol support
- **Multi-PLC**: Concurrent/periodic polling across multiple PLCs
- **Rate Control**: Configurable sampling rate down to milliseconds
- **Preprocessing**: Expression-based transforms & filtering before persistence
- **Resilience**: Reconnect and timeout retries
- **Queues**: RabbitMQ, Kafka, or local in-process queue for burst handling
- **Time-Series Database**: Optimized for high-frequency time-series data collection with batch writes
- **Conditional Acquisition**: Support for start/end event acquisition based on trigger conditions
- **Logging**: Pluggable strategies for auditability and troubleshooting
- **Dynamic Config**: JSON/DB-driven measurements, fields, frequencies, and triggers
- **Cross-Platform**: .NET 8.0 on Win/Linux/macOS

## 🏗️ Architecture

- **DataAcquisition.Domain** – domain models & enums
- **DataAcquisition.Application** – interfaces & service contracts
- **DataAcquisition.Infrastructure** – default implementations
- **DataAcquisition.Gateway** – reference implementation (HslCommunication-based)

### 🧰 Extensibility (Interfaces)

- `IOperationalEventsService` – runtime event & log recording
- `IDeviceConfigService` – device config loader (JSON/DB/others)
- `IPlcClientService` – low-level PLC communication
- `IPlcClientFactory` – custom PLC client factory
- `IDataProcessingService` – preprocessing for sampled data
- `IDataStorageService` – time-series database persistence
- `IQueueService` – message queue producer

**Integration**

1. Register your implementations in `Program.cs`.
2. Build and run; adjust configs as needed.

## 🚀 Quick Start

### 🌐 Requirements

- .NET 8.0 SDK
- Optional: RabbitMQ or Kafka
- Optional: Time-series database (e.g., InfluxDB, TimescaleDB, etc.)

### ⬇️ Install

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
dotnet restore
dotnet build
```

### ▶️ Run

```bash
dotnet run --project DataAcquisition.Gateway
```

Default endpoint: `http://localhost:8000`

## 🗂️ Repository Layout

```text
DataAcquisition/
├── DataAcquisition.Application/      # Interfaces & contracts
│   └── Abstractions/
├── DataAcquisition.Domain/           # Domain models & enums
│   ├── Clients/
│   ├── Models/
│   └── OperationalEvents/
├── DataAcquisition.Infrastructure/   # Default implementations
│   ├── Clients/
│   ├── DataAcquisitions/
│   ├── DataProcessing/
│   ├── DataStorages/
│   ├── DeviceConfigs/
│   ├── OperationalEvents/
│   └── Queues/
├── DataAcquisition.Gateway/          # Web gateway sample
│   ├── BackgroundServices/
│   ├── Configs/
│   ├── Controllers/
│   ├── Hubs/
│   ├── Models/
│   ├── Views/
│   └── wwwroot/
├── DataAcquisition.sln
├── README.md
└── README.en.md
```

## 📝 Configuration

JSON configs under `DataAcquisition.Gateway/Configs` define IPs, registers, data types, triggers, and target measurements. The default loader reads JSON; to use DB or other sources, implement `IDeviceConfigService`.

### 📐 Schema (illustrative YAML)

```yaml
# For illustration only
IsEnabled: true
Code: string # PLC code
Host: string # PLC IP
Port: number # Port
Type: Mitsubishi|Inovance|BeckhoffAds
HeartbeatMonitorRegister: string # [optional]
HeartbeatPollingInterval: number # [optional] ms
Channels: # acquisition channels, each channel is an independent acquisition task
  - ChannelName: string
    ConditionalAcquisition: # [optional] conditional acquisition config, null means unconditional
      Register: string # [optional] trigger address
      DataType: ushort|uint|ulong|short|int|long|float|double # [optional]
      Start:
        TriggerMode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge
        TimestampField: string # [optional] start time field name
      End:
        TriggerMode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge
        TimestampField: string # [optional] end time field name
    EnableBatchRead: bool
    BatchReadRegister: string
    BatchReadLength: int
    Measurement: string # measurement name (table/measurement identifier in time-series database)
    BatchSize: int # 1 = row-by-row
    AcquisitionInterval: int # [optional] acquisition interval (ms), 0 = highest frequency (no delay), default 100
    DataPoints:
      - FieldName: string # field name (field name for storing values in time-series database)
        Register: string
        Index: int
        StringByteLength: int
        Encoding: UTF8|GB2312|GBK|ASCII
        DataType: ushort|uint|ulong|short|int|long|float|double|string|bool
        EvalExpression: string # variable 'value' holds the raw reading
```

### 🔢 Enums

- **Type**
  - `Mitsubishi`: Mitsubishi PLC
  - `Inovance`: Inovance PLC
  - `BeckhoffAds`: Beckhoff ADS

- **ConditionalAcquisition.Start.TriggerMode / ConditionalAcquisition.End.TriggerMode**
  - `Always`: sample unconditionally
  - `ValueIncrease`: sample when the register increases
  - `ValueDecrease`: sample when the register decreases
  - `RisingEdge`: trigger on 0 → 1 transition
  - `FallingEdge`: trigger on 1 → 0 transition

- **ConditionalAcquisition.DataType / DataPoints.DataType**
  - `ushort`, `uint`, `ulong`, `short`, `int`, `long`, `float`, `double`
  - `string` (DataPoints only)
  - `bool` (DataPoints only)

- **Encoding**
  - `UTF8`, `GB2312`, `GBK`, `ASCII`

- **ConditionalAcquisition.Start.TimestampField / ConditionalAcquisition.End.TimestampField**
  - Field name for recording start or end time

### 🔄 Conditional Acquisition & CycleId Mechanism

When `ConditionalAcquisition` is configured, the system performs **conditional acquisition**, determining when to start and end acquisition based on PLC register states.

**Note**: All acquisitions (including unconditional ones) generate a `cycle_id` for data tracking and management.

#### How It Works

1. **Start Event**:
   - When the Start trigger condition is met (e.g., RisingEdge: 0 → 1), the system will:
     - Generate a unique `cycle_id` (GUID format)
     - Insert a new record with all data points, `cycle_id`, and start time
     - Save the acquisition cycle state in memory

2. **End Event**:
   - When the End trigger condition is met (e.g., FallingEdge: 1 → 0), the system will:
     - Retrieve the corresponding `cycle_id` from memory
     - **Write a new data point** with `event_type="end"` tag
     - Associate with the Start event via the `cycle_id` tag
     - If the corresponding cycle is not found (abnormal case), log an error and skip

#### Advantages

- **Time-Series Database Features**: Aligns with time-series database design, storing all events as independent data points with complete history
- **Precise Matching**: Each acquisition cycle has a unique identifier (cycle_id), ensuring correct Start and End association
- **Easy Tracking**: Query complete acquisition cycles via the `cycle_id` tag
- **High-Performance Writes**: Time-series databases are optimized for high-frequency time-series data writes with batch support

#### Time-Series Database Data Structure

All collected data is written to the time-series database using the following structure:

**Data Point Structure**:

- **Measurement**: Measurement name (table/measurement identifier in time-series database)
- **Tags** (for querying and grouping):
  - `device_code`: Device code
  - `channel_name`: Channel name
  - `cycle_id`: Unique acquisition cycle identifier (GUID)
  - `event_type`: Event type ("start" | "end" | "data")
- **Fields** (for storing values):
  - All collected data point values
  - Timestamp fields (e.g., start_time, end_time)
- **Timestamp**: Collection time

**Example (Time-Series Database Line Protocol Format)**:

```
measurement,device_code=PLC01,channel_name=Channel1,cycle_id=xxx,event_type=start field1=value1,field2=value2 1234567890000000000
measurement,device_code=PLC01,channel_name=Channel1,cycle_id=xxx,event_type=end end_time=1234567890000000000 1234567891000000000
```

**Query Examples** (using InfluxDB as an example):

- Query all events for a specific cycle_id: `from(bucket: "plc_data") |> filter(fn: (r) => r["cycle_id"] == "xxx")`
- Query Start events: `from(bucket: "plc_data") |> filter(fn: (r) => r["event_type"] == "start")`

#### Typical Use Cases

1. **Production Cycle Management**
   - Scenario: Record start time when production begins, record end time when production ends
   - Config: Start uses RisingEdge (production start signal 0 → 1), End uses FallingEdge (production end signal 1 → 0)
   - Data: Record production start time, end time, output, quality, etc.

2. **Equipment Operation Status Monitoring**
   - Scenario: Record operation start time when equipment starts, record stop time when equipment stops
   - Config: Start uses RisingEdge (operation signal 0 → 1), End uses FallingEdge (operation signal 1 → 0)
   - Data: Record equipment operation duration, energy consumption, failure count, etc.

3. **Batch Management**
   - Scenario: Insert record when batch starts, insert record when batch ends
   - Config: Start uses ValueIncrease (batch number increases), End uses ValueDecrease (batch number decreases)
   - Data: Record batch number, start time, end time, batch output, etc.

4. **Process Parameter Collection**
   - Scenario: Collect initial parameters when process starts, collect final parameters when process ends
   - Config: Start uses RisingEdge (process start signal), End uses FallingEdge (process end signal)
   - Data: Record temperature, pressure, speed, and other process parameter changes

5. **Quality Inspection Cycle**
   - Scenario: Record inspection parameters when inspection starts, record inspection results when inspection ends
   - Config: Start uses RisingEdge (inspection start signal), End uses FallingEdge (inspection end signal)
   - Data: Record inspection time, inspection results, pass rate, etc.

### 🧮 EvalExpression

Use an expression to transform the raw reading before persistence. The variable `value` represents the raw register value, e.g., `"value / 1000.0"`. Empty string means no transform.

### 📘 Example

`DataAcquisition.Gateway/Configs/M01C123.json`:

```json
{
  "IsEnabled": true,
  "Code": "M01C123",
  "Host": "192.168.1.110",
  "Port": 4104,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D6061",
  "HeartbeatPollingInterval": 2000,
  "Channels": [
    {
      "ChannelName": "M01C01",
      "Measurement": "m01c01_sensor",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 70,
      "BatchSize": 1,
      "AcquisitionInterval": 100,
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
      "ConditionalAcquisition": null
    },
    {
      "ChannelName": "M01C02",
      "Measurement": "m01c01_recipe",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6100",
      "BatchReadLength": 200,
      "BatchSize": 1,
      "AcquisitionInterval": 0,
      "DataPoints": [
        {
          "FieldName": "up_set_temp",
          "Register": "D6102",
          "Index": 2,
          "DataType": "short"
        },
        {
          "FieldName": "down_set_temp",
          "Register": "D6104",
          "Index": 4,
          "DataType": "short",
          "EvalExpression": "value / 1000.0"
        }
      ],
      "ConditionalAcquisition": {
        "Register": "D6200",
        "DataType": "short",
        "Start": {
          "TriggerMode": "RisingEdge",
          "TimestampField": "start_time"
        },
        "End": {
          "TriggerMode": "FallingEdge",
          "TimestampField": "end_time"
        }
      }
    }
  ]
}
```

## 🔗 API

### PLC Connection Status

- `GET /api/DataAcquisition/GetPlcConnectionStatus`
  Returns a dictionary of PLC connection states.

### Write PLC Registers

- `POST /api/DataAcquisition/WriteRegister`
  Example (batch writes, `dataType` specifies the value type):

```json
{
  "plcCode": "PLC01",
  "items": [
    { "address": "D100", "dataType": "short", "value": 1 },
    { "address": "D101", "dataType": "int", "value": 2 }
  ]
}
```

## 📦 Dependencies (NuGet)

### Core

- Microsoft.Extensions.Caching.Memory `9.0.2`
- NCalcAsync `5.4.0`
- Newtonsoft.Json `13.0.3`

### Sample Implementation

- InfluxDB.Client `2.0.0` (Time-series database client, can be replaced with other time-series database implementations as needed)
- HslCommunication `12.2.0`
- Microsoft.AspNetCore.SignalR `1.2.0`
- Serilog.AspNetCore `9.0.0`
- Serilog.Sinks.Console `6.0.0`
- Serilog.Sinks.File `7.0.0`

## 💻 Development & Registration

Register services in `Program.cs`:

```csharp
builder.Services.AddSingleton<OpsEventChannel>();
builder.Services.AddSingleton<IOpsEventBus>(sp => sp.GetRequiredService<OpsEventChannel>());
builder.Services.AddSingleton<IOperationalEventsService, OperationalEventsService>();
builder.Services.AddSingleton<IPlcClientFactory, PlcClientFactory>();
builder.Services.AddSingleton<IDataStorageService, InfluxDbDataStorageService>();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();
builder.Services.AddSingleton<IPlcStateManager, PlcStateManager>();
builder.Services.AddSingleton<IAcquisitionStateManager, AcquisitionStateManager>();  // Acquisition cycle state management
builder.Services.AddSingleton<ITriggerEvaluator, TriggerEvaluator>();                 // Trigger condition evaluation
builder.Services.AddSingleton<IChannelCollector, ChannelCollector>();
builder.Services.AddSingleton<IDataAcquisitionService, DataAcquisitionService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
builder.Services.AddHostedService<QueueHostedService>();
builder.Services.AddHostedService<OpsEventBroadcastWorker>();
```

## 🚢 Deployment

Build self-contained executables:

```bash
dotnet publish DataAcquisition.Gateway -c Release -r win-x64  --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r linux-x64 --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r osx-x64  --self-contained true
```

Copy the `publish` folder to the target machine and run the platform-specific binary.

## 🙏 Contributing

PRs are welcome. Please ensure tests pass and avoid breaking changes.

## 📜 License

MIT — see [LICENSE](LICENSE).
