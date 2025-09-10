# 🛰️ PLC Data Acquisition System

[![Stars](https://img.shields.io/github/stars/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/stargazers)
[![Forks](https://img.shields.io/github/forks/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/network/members)
[![License](https://img.shields.io/github/license/liuweichaox/DataAcquisition.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](#)

[中文](README.md) | **English**

## 📙 Overview
The PLC Data Acquisition System collects real-time operational data from programmable logic controllers and forwards the results to message queues and databases, supporting equipment monitoring, performance analysis, and fault diagnosis.

## 💡 Key Features
- Efficient communication using the Modbus TCP protocol.
- Message queues (RabbitMQ, Kafka, or a local queue) handle high-throughput acquisition results.
- Data can be stored in SQLite or various cloud databases.
- Custom logging strategies assist with troubleshooting and auditing.
- Periodic acquisition from multiple PLCs is supported.
- Disconnection and timeout retries maintain stability.
- Data preprocessing transforms acquisition results before storage.
- Acquisition frequency is configurable down to milliseconds.
- Configuration files define table structures, column names, and sampling frequency.
- Built on .NET 8.0 and runs on Windows, Linux, and macOS.

## 🏗️ Architecture Overview
- **DataAcquisition.Domain**: domain models and enums.
- **DataAcquisition.Application**: service contracts and interfaces.
- **DataAcquisition.Infrastructure**: default implementations.
- **DataAcquisition.Gateway**: a reference gateway built with HslCommunication, serving as an example implementation.

### 🧰 How to customize implementation
1. Implement `IPlcClient` and `IPlcClientFactory` to support other PLC protocols or communication methods.
2. Implement `IDataStorage` to use a different database or persistence layer.
3. Implement `IQueue` to integrate custom message queues.
4. Implement `IOperationalEvents` to record errors, logs, or other operational events.
5. Implement `IDataProcessingService` to preprocess data before storage.
6. Register these implementations in `Program.cs`, replacing the default dependencies.
7. Build and run the project, adjusting configuration files as needed.

## 🌐 Environment Requirements
- .NET 8.0 SDK
- Optional: RabbitMQ or Kafka (for message queues)
- Optional: SQLite or other database drivers

## 🔧 Installation
### ⬇️ Clone the repository
```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
```
### 🔄 Restore dependencies
```bash
dotnet restore
```

## 📝 Configuration
The `DataAcquisition.Gateway/Configs` directory stores JSON files that correspond to database tables. Each file defines PLC addresses, registers, data types, and other settings.

### 📐 Configuration structure
Configuration files use JSON format; the structure is described below using YAML:

```yaml
# Configuration structure (for illustration only)
IsEnabled: true                 # Enable this configuration
Code: string                    # PLC identifier
Host: string                    # PLC IP address
Port: number                    # PLC communication port
Type: Mitsubishi|Inovance       # PLC type
HeartbeatMonitorRegister: string # [Optional] register for heartbeat monitoring
HeartbeatPollingInterval: number # [Optional] heartbeat polling interval (milliseconds)
Modules:
  - ChamberCode: string         # Channel identifier
    Trigger:
      Mode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge # Trigger mode
      Register: string          # Trigger register address
      DataType: ushort|uint|ulong|short|int|long|float|double # Trigger register data type
      Operation: Insert|Update  # Data operation type
      TimeColumnName: string    # [Optional] column name for timestamp
    BatchReadRegister: string   # Start register for batch reading
    BatchReadLength: int        # Number of registers to read
    TableName: string           # Target database table
    BatchSize: int              # Number of records per batch (1 inserts one by one)
    DataPoints:
      - ColumnName: string      # Column name in the database
        Index: int              # Register index
        StringByteLength: int   # Byte length for string values
        Encoding: UTF8|GB2312|GBK|ASCII # Character encoding
        DataType: ushort|uint|ulong|short|int|long|float|double|string|bool # Data type of the register
        EvalExpression: string  # Expression for value conversion, use 'value' for the raw value
```

### 🔢 Enum descriptions
- **Type**
  - `Mitsubishi`: Mitsubishi PLC
  - `Inovance`: Inovance PLC
- **Trigger.Mode**
  - `Always`: always sample
  - `ValueIncrease`: sample when the register value increases
  - `ValueDecrease`: sample when the register value decreases
  - `RisingEdge`: fires when the register changes from 0 to 1
  - `FallingEdge`: fires when the register changes from 1 to 0
- **Trigger.DataType / DataPoints.DataType**
  - `ushort`, `uint`, `ulong`
  - `short`, `int`, `long`
  - `float`, `double`
  - `string`, `bool` (DataPoints only)
- **Encoding**
  - `UTF8`, `GB2312`, `GBK`, `ASCII`
- **Trigger.Operation**
  - `Insert`: insert a new record
  - `Update`: update an existing record
- **Trigger.TimeColumnName**
  - Optional column name for timestamps. For an `Update` operation, this column
    receives the new timestamp while the start-time column from the associated
    `Insert` trigger is used to locate the record.

### 🧮 EvalExpression usage
`EvalExpression` converts the raw register value before storage. The expression may reference the variable `value` representing the raw number and can use basic arithmetic. For example, `"value / 1000.0"` scales the value; leave it empty to skip conversion.

### 🗒️ Sample configuration
The file `DataAcquisition.Gateway/Configs/M01C123.json` illustrates a typical configuration.

```json
{
  "IsEnabled": true,
  "Code": "M01C123",
  "Host": "192.168.1.110",
  "Port": 4104,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D6061",
  "HeartbeatPollingInterval": 2000,
  "Modules": [
    {
      "ChamberCode": "M01C01",
      "Trigger": {
        "Mode": "Always",
        "Register": "D6000",
        "DataType": "short",
        "Operation": "Insert"
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
        "Register": "D6200",
        "DataType": "short",
        "Operation": "Insert",
        "TimeColumnName": "start_time"
      },
      "BatchReadRegister": "D6100",
      "BatchReadLength": 200,
      "TableName": "m01c01_recipe",
      "BatchSize": 1,
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
    },
    {
      "ChamberCode": "M01C02",
      "Trigger": {
        "Mode": "FallingEdge",
        "Register": "D6200",
        "DataType": "short",
        "Operation": "Update",
        "TimeColumnName": "end_time"
      },
      "BatchReadRegister": null,
      "BatchReadLength": 0,
      "TableName": "m01c01_recipe",
      "BatchSize": 1,
      "DataPoints": null
    }
  ]
}
```

## ▶️ Run
Make sure the .NET 8.0 SDK is installed.

```bash
dotnet build
dotnet run --project DataAcquisition.Gateway
```

The service listens on http://localhost:8000 by default.

## 💻 Development
### 🔧 System configuration
Register the `IDataAcquisition` instance in `Program.cs` to manage acquisition tasks.

```csharp
builder.Services.AddSingleton<IOperationalEvents, OperationalEvents>();
builder.Services.AddSingleton<IPlcClientFactory, PlcClientFactory>();
builder.Services.AddSingleton<IDataStorageFactory, DataStorageFactory>();
builder.Services.AddSingleton<IQueueFactory, QueueFactory>();
builder.Services.AddSingleton<IDataAcquisition, DataAcquisition>();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
```

### 🗂️ Repository structure
- `DataAcquisition.Domain`: domain models and enums.
- `DataAcquisition.Application`: service contracts and interfaces.
- `DataAcquisition.Infrastructure`: interface implementations.
- `DataAcquisition.Gateway`: example gateway layer.

### 🔨 Build
```bash
dotnet build
```

## 🔗 API
### 📶 Get PLC connection status
- `GET /api/DataAcquisition/GetPlcConnectionStatus`

The endpoint returns a dictionary of PLC connection states.

### ✏️ Write to PLC register
- `POST /api/DataAcquisition/WriteRegister`

Request example (batch write, `dataType` specifies the value type for each item):

```json
{
  "plcCode": "PLC01",
  "items": [
    { "address": "D100", "dataType": "short", "value": 1 },
    { "address": "D101", "dataType": "int", "value": 2 }
  ]
}
```

## 🚢 Deployment
Use `dotnet publish` to generate self-contained executables for different platforms:

```bash
dotnet publish DataAcquisition.Gateway -c Release -r win-x64 --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r linux-x64 --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r osx-x64 --self-contained true
```

Copy the contents of the `publish` folder to the target environment and run the platform-specific executable.

## 🙏 Contribution
Contributions are welcome via Pull Requests. Ensure all relevant tests pass and avoid introducing breaking changes.

## 📜 License
This project is licensed under the MIT License; see [LICENSE](LICENSE) for details.

