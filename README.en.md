# 🛰️ PLC Data Acquisition System

[![Stars](https://img.shields.io/github/stars/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/stargazers)
[![Forks](https://img.shields.io/github/forks/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/network/members)
[![License](https://img.shields.io/github/license/liuweichaox/DataAcquisition.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](#)

**[中文](README.md) | English**

## 📙 Overview

This system collects real-time data from PLCs and pushes results to **message queues** and **databases** to support **online monitoring, performance analytics, and fault diagnostics**. It’s built on .NET 8.0 and runs on Windows, Linux, and macOS.

## 💡 Key Features

- **Reliable I/O**: Modbus TCP (sample) with extensible protocol support
- **Multi-PLC**: Concurrent/periodic polling across multiple PLCs
- **Rate Control**: Configurable sampling rate down to milliseconds
- **Preprocessing**: Expression-based transforms & filtering before persistence
- **Resilience**: Reconnect and timeout retries
- **Queues**: RabbitMQ, Kafka, or local in-process queue for burst handling
- **Storage**: SQLite and various cloud databases
- **Logging**: Pluggable strategies for auditability and troubleshooting
- **Dynamic Config**: JSON/DB-driven tables, columns, frequencies, and triggers
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
- `IDataStorageService` – database persistence
- `IQueueService` – message queue producer

**Integration**

1. Register your implementations in `Program.cs`.
2. Build and run; adjust configs as needed.

## 🚀 Quick Start

### 🌐 Requirements

- .NET 8.0 SDK
- Optional: RabbitMQ or Kafka
- Optional: SQLite or other DB drivers

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

JSON configs under `DataAcquisition.Gateway/Configs` define IPs, registers, data types, triggers, and target tables. The default loader reads JSON; to use DB or other sources, implement `IDeviceConfigService`.

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
    Lifecycle: # optional start/end triggers
      Start:
        Trigger:
          Mode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge
          Register: string
          DataType: ushort|uint|ulong|short|int|long|float|double
        Operation: Insert|Update
        StampColumn: string # [optional] start time column
      End:
        Trigger:
          Mode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge
          Register: string
          DataType: ushort|uint|ulong|short|int|long|float|double
        Operation: Insert|Update
        StampColumn: string # [optional] end time column
    EnableBatchRead: bool
    BatchReadRegister: string
    BatchReadLength: int
    TableName: string
    BatchSize: int # 1 = row-by-row
    DataPoints:
      - ColumnName: string
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

- **Trigger.Mode**
  - `Always`: sample unconditionally
  - `ValueIncrease`: sample when the register increases
  - `ValueDecrease`: sample when the register decreases
  - `RisingEdge`: trigger on 0 → 1 transition
  - `FallingEdge`: trigger on 1 → 0 transition

- **Trigger.DataType / DataPoints.DataType**
  - `ushort`, `uint`, `ulong`, `short`, `int`, `long`, `float`, `double`
  - `string` (DataPoints only)
  - `bool` (DataPoints only)

- **Encoding**
  - `UTF8`, `GB2312`, `GBK`, `ASCII`

- **Lifecycle.Start.Operation / Lifecycle.End.Operation**
  - `Insert`: append a new row
  - `Update`: modify an existing row

- **Lifecycle.Start.StampColumn / Lifecycle.End.StampColumn**
  - Column name for recording start or end time.

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
      "ChannelId": "01J9Z7R9C2M01C01",
      "ChannelName": "M01C01",
      "TableName": "m01c01_sensor",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 70,
      "BatchSize": 1,
      "DataPoints": [
        { "ColumnName": "up_temp", "Register": "D6002", "Index": 2, "DataType": "short" },
        { "ColumnName": "down_temp", "Register": "D6004", "Index": 4, "DataType": "short", "EvalExpression": "value / 1000.0" }
      ],
      "Lifecycle": null
    },
    {
      "ChannelId": "01J9Z7R9C2M01C02",
      "ChannelName": "M01C02",
      "TableName": "m01c01_recipe",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6100",
      "BatchReadLength": 200,
      "BatchSize": 1,
      "DataPoints": [
        { "ColumnName": "up_set_temp", "Register": "D6102", "Index": 2, "DataType": "short" },
        { "ColumnName": "down_set_temp", "Register": "D6104", "Index": 4, "DataType": "short", "EvalExpression": "value / 1000.0" }
      ],
      "Lifecycle": {
        "Start": {
          "Trigger": { "Mode": "RisingEdge", "Register": "D6200", "DataType": "short" },
          "Operation": "Insert",
          "StampColumn": "start_time"
        },
        "End": {
          "Trigger": { "Mode": "FallingEdge", "Register": "D6200", "DataType": "short" },
          "Operation": "Update",
          "StampColumn": "end_time"
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

### Sample

- Dapper `2.1.66`
- HslCommunication `12.2.0`
- MySqlConnector `2.4.0`
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
builder.Services.AddSingleton<IDataStorageFactory, DataStorageFactory>();
builder.Services.AddSingleton<IQueueFactory, QueueFactory>();
builder.Services.AddSingleton<IDataAcquisitionService, DataAcquisitionService>();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
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
