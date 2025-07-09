# PLC 数据采集系统

## 📌 项目概述

本项目旨在通过动态收集来自 PLC（可编程逻辑控制器）的数据，为用户提供实时监控和分析工业设备运行状态的能力。支持多种 PLC 类型、实时数据采集、消息队列、高效数据存储等功能，适用于工业自动化过程中的监控与控制、设备性能分析及故障诊断。

---

## 🚀 核心功能

- **高效通讯**：基于 Modbus TCP 协议，实现稳定的高效通讯
- **消息队列**：支持数据缓存至 RabbitMQ、Kafka 或 本地消息队列，用于高并发数据采集
- **数据存储**：支持存储至本地 SQLite 数据库或云存储
- **日志记录**：支持自定义日志记录方式，便于问题排查与系统监控
- **多 PLC 数据采集**：支持从多个 PLC 周期性地采集实时数据
- **错误处理**：支持断线重连与超时重试，确保系统稳定运行
- **频率控制**：可配置采集频率，支持毫秒级控制
- **动态配置**：通过配置定义采集表、列名、频率，支持自定义数据点和采集方式
- **多平台支持**：兼容 .NET Standard 2.0 和 2.1
- **操作系统**：支持 Windows、Linux、macOS

---

## 🛠️ 安装与使用

### 1️⃣ 克隆仓库

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
```

### 2️⃣ 配置文件

在 `Configs` 文件夹中，每个表对应一个独立的 JSON 文件，您可以根据需要修改配置。配置文件定义了 PLC 信息、寄存器地址、数据类型等内容。

#### 配置文件定义

- **IsEnabled**: 是否启用该配置。
- **Code**: 采集器代码，用于标识不同的采集器。
- **Host**: PLC IP 地址。
- **Port**: PLC 端口。
- **DriverType**: PLC 驱动类型，支持 `MelsecA1ENet`、`MelsecA1EAsciiNet`、`InovanceTcpNet`。
- **HeartbeatMonitorRegister**: 心跳监控寄存器地址。
- **HeartbeatPollingInterval**: 心跳监控间隔（毫秒）。
- **StorageType**: 数据存储类型，支持 `SQLite`、`MySQL`、`PostgreSQL`、`SQLServer`。
- **ConnectionString**: 数据库连接字符串。
- **Modules**: 采集模块配置。
  - **ChamberCode**: 采集通道代码，用于标识不同的采集通道。
  - **Trigger**: 触发配置。
    - **Mode**: 触发模式，支持 `Always`（一直触发）、`ValueIncrease`（数值增加时触发）、`ValueDecrease`（数值减少触发）、`RisingEdge`（上升沿触发(表示从 0 变成 1 时采集)）、`FallingEdge`（下降沿触发(表示从 1 变成 0 时采集)）。
    - **Register**: 触发寄存器地址。
    - **DataType**: 触发寄存器数据类型。
  - **BatchReadRegister**: 批量读取寄存器地址。
  - **BatchReadLength**: 批量读取寄存器长度。
  - **TableName**: 数据库表名。
  - **DataPoints**: 数据点配置。
    - **ColumnName**: 数据库列名。
    - **Index**: 寄存器索引。
    - **StringByteLength**: 字符串字节长度。
    - **Encoding**: 字符串编码，支持 `UTF8`、`GB2312`、`GBK`、`ASCII`。
    - **DataType**: 寄存器数据类型。
    - **EvalExpression**: 数据转换表达式，支持简单的数学表达式，例如 `value / 1000.0`。

---

**示例配置** (`Configs/M01_Metrics.json`):

```json
{
  "IsEnabled": true,
  "Code": "M01C123",
  "Host": "192.168.1.110",
  "Port": 4104,
  "DriverType": "MelsecA1EAsciiNet",
  "HeartbeatMonitorRegister": "D6061",
  "HeartbeatPollingInterval": 2000,
  "StorageType": "MySQL",
  "ConnectionString": "Server=127.0.0.1;Database=daq;Uid=root;Pwd=123456;Connect Timeout=30;SslMode=None;",
  "Modules": [
    {
      "ChamberCode": "M01C01",
      "Trigger": {
        "Mode": "Always",
        "Register": null,
        "DataType": null,
        "PollInterval": 0
      },
      "BatchReadRegister": "D6000",
      "BatchReadLength": 70,
      "TableName": "m01c01_sensor",
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
        "DataType": null,
        "PollInterval": 0
      },
      "BatchReadRegister": "D6100",
      "BatchReadLength": 200,
      "TableName": "m01c02_sensor",
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

## ⚙️ 配置 `DataAcquisitionService`

在 `Startup.cs` 中配置 `IDataAcquisitionService` 实例，负责管理数据采集任务。

```csharp
builder.Services.AddSingleton<IMessageService, MessageService>();
builder.Services.AddSingleton<IPlcDriverFactory, PlcDriverFactory>();
builder.Services.AddSingleton<IDataStorageFactory, DataStorageFactory>();
builder.Services.AddSingleton<IQueueManagerFactory, QueueManagerFactory>();
builder.Services.AddSingleton<IDataAcquisitionService, DataAcquisitionService>();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
```

### 配置解释

- **`IDataAcquisitionConfigService dataAcquisitionConfigService`**：配置服务，负责读取和解析数据采集的配置文件（例如，采集频率、数据存储方式等）。
- **`IPlcClientFactory plcClientFactory`**：初始化 PLC 客户端，通过 IP 地址和端口连接到 PLC。
- **`IDataStorageFactory dataStorageFactory`**：初始化据存储服务，用于采集的数据存储到数据库，支持本地存储和云存储。
- **`IQueueManagerFactory queueManagerFactory`**：初始化消息队列管理器，支持 RabbitMQ 或 Kafka。
- **`IMessageService messageService`**： 数据采集异常消息处理委托，可以用于日志记录或报警。

---

## 📑 API 文档

### 获取 PLC 连接状态

- **GET** `/api/DataAcquisition/GetPlcConnectionStatus`
- **返回**：`PLC 连接状态字典`

---

## 🤝 贡献

如果您想为该项目贡献代码，欢迎提交 Pull Request！在提交之前，请确保代码通过了所有单元测试，并且没有引入任何破坏性变化。

## 📄 许可

本项目使用 MIT 许可证，详情请参阅 [LICENSE](LICENSE) 文件。

---

感谢您使用 PLC 数据采集系统！如有问题，欢迎提出 issue 或进行讨论。 🎉

---
