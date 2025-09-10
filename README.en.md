# 🛰️ PLC Data Acquisition System

[![Stars](https://img.shields.io/github/stars/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/stargazers)
[![Forks](https://img.shields.io/github/forks/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/network/members)
[![License](https://img.shields.io/github/license/liuweichaox/DataAcquisition.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](#)

[中文](README.md) | **English**

## 📙 Overview
The PLC Data Acquisition System collects real-time data from programmable logic controllers and forwards the results to message queues and databases, enabling equipment monitoring, performance analysis, and fault diagnosis.

## 💡 Key Features
- Efficient communication using the Modbus TCP protocol
- Write acquisition results to RabbitMQ, Kafka, or a local queue
- Store data in SQLite or various cloud databases
- Custom logging strategies for troubleshooting and auditing
- Periodic acquisition from multiple PLCs
- Disconnection and timeout retries maintain stability
- Data preprocessing before persistence
- Acquisition frequency configurable down to milliseconds
- Dynamic configuration of table structures, column names, and sampling frequency via JSON files or databases
- Built on .NET 8.0 and runs on Windows, Linux, and macOS

## 🏗️ Architecture
- **DataAcquisition.Domain** – domain models and enums
- **DataAcquisition.Application** – service contracts and interfaces
- **DataAcquisition.Infrastructure** – default implementations
- **DataAcquisition.Gateway** – reference gateway using HslCommunication

## 📦 Dependencies
### Framework
- [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory) 9.0.2
- [NCalcAsync](https://www.nuget.org/packages/NCalcAsync) 5.4.0
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) 13.0.3

### Example
- [Dapper](https://www.nuget.org/packages/Dapper) 2.1.66
- [HslCommunication](https://www.nuget.org/packages/HslCommunication) 12.2.0
- [MySqlConnector](https://www.nuget.org/packages/MySqlConnector) 2.4.0
- [Microsoft.AspNetCore.SignalR](https://www.nuget.org/packages/Microsoft.AspNetCore.SignalR) 1.2.0
- [Serilog.AspNetCore](https://www.nuget.org/packages/Serilog.AspNetCore) 9.0.0
- [Serilog.Sinks.Console](https://www.nuget.org/packages/Serilog.Sinks.Console) 6.0.0
- [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File) 7.0.0

## 🔧 Installation
### Prerequisites
- .NET 8.0 SDK
- Optional: RabbitMQ or Kafka
- Optional: SQLite or other database drivers

### Clone and restore
```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
dotnet restore
```

## ⚙️ Configuration
- Default device configuration is stored as JSON files under `DataAcquisition.Gateway/Configs`
- To load configuration from a database, implement `IDeviceConfigService`

Example JSON:
```json
{
  "Code": "M01C123",
  "Host": "192.168.1.110",
  "Port": 4104
}
```

## ▶️ Usage
Build and run the gateway project:
```bash
dotnet build
dotnet run --project DataAcquisition.Gateway
```
The service listens on `http://localhost:8000` by default.

## 🔗 API
### Get PLC connection status
`GET /api/DataAcquisition/GetPlcConnectionStatus`

### Write to PLC register
`POST /api/DataAcquisition/WriteRegister`
```json
{
  "plcCode": "PLC01",
  "items": [
    { "address": "D100", "dataType": "short", "value": 1 },
    { "address": "D101", "dataType": "int", "value": 2 }
  ]
}
```

## 💻 Development
Register services in `Program.cs` to manage acquisition tasks:
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

## 🙏 Contribution
Contributions are welcome via Pull Requests. Please ensure relevant tests pass and avoid breaking changes.

## 📜 License
This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
