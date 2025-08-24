# 📡 PLC Data Acquisition System

[![Stars](https://img.shields.io/github/stars/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/stargazers)
[![Forks](https://img.shields.io/github/forks/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/network/members)
[![License](https://img.shields.io/github/license/liuweichaox/DataAcquisition.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0%20%7C%202.1-512BD4?logo=dotnet)](#)

[中文](README.md) | **English**

## 📘 Overview
The PLC Data Acquisition System collects real-time operational data from programmable logic controllers and forwards the results to message queues and databases, supporting equipment monitoring, performance analysis, and fault diagnosis.

## ✨ Key Features
- Efficient communication using the Modbus TCP protocol ensures stable data exchange.
- Message queues such as RabbitMQ, Kafka, or a local queue handle high-throughput acquisition results.
- Data can be stored in SQLite or various cloud databases.
- Custom logging strategies assist with troubleshooting and auditing.
- Periodic acquisition from multiple PLCs is supported.
- Disconnection and timeout retries are available to maintain stability.
- Acquisition frequency is configurable down to milliseconds.
- Configuration files define table structures, column names, and sampling frequency.
- Compatible with .NET Standard 2.0/2.1 and runs on Windows, Linux, and macOS.

## 🛠️ Installation

### 📥 Clone the repository
```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
```

### ⚙️ Configuration files
The `DataAcquisition.Gateway/Configs` directory stores JSON files that correspond to database tables. Each file defines PLC addresses, registers, data types, and other settings and can be adjusted as needed.

#### 📑 Configuration fields

| Field | Type | Description |
|-------|------|-------------|
| `IsEnabled` | `bool` | Whether the configuration is enabled. |
| `Code` | `string` | PLC identifier. |
| `Host` | `string` | PLC IP address. |
| `Port` | `int` | PLC port. |
| `HeartbeatMonitorRegister` | `string` | Register address for heartbeat monitoring. |
| `HeartbeatPollingInterval` | `int` | Heartbeat polling interval in milliseconds. |
| `ConnectionString` | `string` | Database connection string. |
| `Modules` | `Module[]` | Acquisition modules. |

##### Module

| Field | Type | Description |
|-------|------|-------------|
| `ChamberCode` | `string` | Channel identifier. |
| `Trigger` | `TriggerConfig` | Trigger settings. |
| `BatchReadRegister` | `string` | Start register for batch reading. |
| `BatchReadLength` | `int` | Number of registers to read. |
| `TableName` | `string` | Target database table. |
| `BatchSize` | `int` | Number of records per batch (`1` inserts one by one). |
| `DataPoints` | `DataPoint[]` | Data point configuration. |

##### TriggerConfig

| Field | Type | Description |
|-------|------|-------------|
| `Mode` | `string` | Trigger mode. |
| `Register` | `string` | Trigger register address. |
| `DataType` | `string` | Data type of the trigger register. |

##### DataPoint

| Field | Type | Description |
|-------|------|-------------|
| `ColumnName` | `string` | Column name in the database. |
| `Index` | `int` | Register index. |
| `StringByteLength` | `int` | Byte length for string values. |
| `Encoding` | `string` | Character encoding. |
| `DataType` | `string` | Data type of the register. |
| `EvalExpression` | `string` | Expression for value conversion, e.g. `value / 1000.0`. |

#### 📚 Enumeration reference
- **Trigger.Mode**
  - `Always`: always sample.
  - `ValueIncrease`: sample when the register value increases.
  - `ValueDecrease`: sample when the register value decreases.
  - `RisingEdge`: sample on a rising edge (0 → 1).
  - `FallingEdge`: sample on a falling edge (1 → 0).
- **DataType**
  - `Trigger.DataType`: `ushort`, `uint`, `ulong`, `short`, `int`, `long`, `float`, `double`.
  - `DataPoints.DataType`: `ushort`, `uint`, `ulong`, `short`, `int`, `long`, `float`, `double`, `string`, `bool`.
- **Encoding**
  - `UTF8`, `GB2312`, `GBK`, `ASCII`.

### 📄 Sample configuration
The file `DataAcquisition.Gateway/Configs/M01C123.json` illustrates a typical configuration.

```json
{
  "IsEnabled": true,
  "Code": "M01C123",
  "Host": "192.168.1.110",
  "Port": 4104,
  "HeartbeatMonitorRegister": "D6061",
  "HeartbeatPollingInterval": 2000,
  "ConnectionString": "Server=127.0.0.1;Database=daq;Uid=root;Pwd=123456;Connect Timeout=30;SslMode=None;",
  "Modules": [
    {
      "ChamberCode": "M01C01",
      "Trigger": {
        "Mode": "Always",
        "Register": null,
        "DataType": null
      },
      "BatchReadRegister": "D6000",
      "BatchReadLength": 70,
      "TableName": "m01c01_sensor",
      "BatchSize": 1,
      "DataPoints": [
        {
          "ColumnName": "up_temp",
          "Index": 2,
          "StringByteLength": 0,
          "Encoding": null,
          "DataType": "short",
          "EvalExpression": ""
        },
        {
          "ColumnName": "down_temp",
          "Index": 4,
          "StringByteLength": 0,
          "Encoding": null,
          "DataType": "short",
          "EvalExpression": "value / 1000.0"
        }
      ]
    },
    {
      "ChamberCode": "M01C02",
      "Trigger": {
        "Mode": "RisingEdge",
        "Register": null,
        "DataType": null
      },
      "BatchReadRegister": "D6100",
      "BatchReadLength": 200,
      "TableName": "m01c02_sensor",
      "BatchSize": 10,
      "DataPoints": [
        {
          "ColumnName": "up_set_temp",
          "Index": 2,
          "StringByteLength": 0,
          "Encoding": null,
          "DataType": "short",
          "EvalExpression": ""
        },
        {
          "ColumnName": "down_set_temp",
          "Index": 4,
          "StringByteLength": 0,
          "Encoding": null,
          "DataType": "short",
          "EvalExpression": "value / 1000.0"
        }
      ]
    }
  ]
}
```

## 🧩 Application setup
Register the `IDataAcquisition` instance in `Startup.cs` to manage acquisition tasks.

```csharp
builder.Services.AddSingleton<IMessage, Message>();
builder.Services.AddSingleton<ICommunicationFactory, CommunicationFactory>();
builder.Services.AddSingleton<IDataStorageFactory, DataStorageFactory>();
builder.Services.AddSingleton<IQueueFactory, QueueFactory>();
builder.Services.AddSingleton<IDataAcquisition, DataAcquisition>();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
```

## 🔌 API

### 📡 Get PLC connection status
- `GET /api/DataAcquisition/GetPlcConnectionStatus`

The endpoint returns a dictionary of PLC connection states.

## 🤝 Contribution
Contributions are accepted via Pull Requests. Ensure all relevant tests pass and avoid breaking changes before submission.

## 📄 License
This project is licensed under the MIT License; see [LICENSE](LICENSE) for details.

