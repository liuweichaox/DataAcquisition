# PLC 数据采集系统

[![Stars](https://img.shields.io/github/stars/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/stargazers)
[![Forks](https://img.shields.io/github/forks/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/network/members)
[![License](https://img.shields.io/github/license/liuweichaox/DataAcquisition.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0%20%7C%202.1-512BD4?logo=dotnet)](#)

**中文** | [English](README.en.md)

## 概述
PLC 数据采集系统用于从可编程逻辑控制器实时收集运行数据，并将结果传递至消息队列和数据库，以支持工业设备监控、性能分析与故障诊断。

## 核心功能
- 高效通讯：基于 Modbus TCP 协议实现稳定的数据传输。
- 消息队列：可将采集结果写入 RabbitMQ、Kafka 或本地队列以处理高并发。
- 数据存储：支持本地 SQLite 数据库及多种云端数据库。
- 日志记录：允许自定义日志策略，便于排查和审计。
- 多 PLC 数据采集：支持同时周期性读取多个 PLC。
- 错误处理：提供断线重连和超时重试机制。
- 频率控制：采集频率可配置，最低支持毫秒级。
- 动态配置：通过配置文件定义表结构、列名和频率。
- 多平台支持：兼容 .NET Standard 2.0 与 2.1，运行于 Windows、Linux 和 macOS。

## 安装

### 克隆仓库
```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
```

### 配置文件
`Configs` 目录包含与数据库表对应的 JSON 文件。每个文件定义 PLC 地址、寄存器、数据类型等信息，可根据实际需求调整。

#### 配置字段
- **IsEnabled**：是否启用该配置。
- **Code**：采集器标识。
- **Host**：PLC IP 地址。
- **Port**：PLC 端口。
- **DriverType**：驱动类型，支持 `MelsecA1ENet`、`MelsecA1EAsciiNet`、`InovanceTcpNet`。
- **HeartbeatMonitorRegister**：心跳监控寄存器地址。
- **HeartbeatPollingInterval**：心跳轮询间隔（毫秒）。
- **StorageType**：数据存储类型，支持 `SQLite`、`MySQL`、`PostgreSQL`、`SQLServer`。
- **ConnectionString**：数据库连接字符串。
- **Modules**：采集模块定义。
  - **ChamberCode**：采集通道代码。
  - **Trigger**：触发配置。
    - **Mode**：触发模式，`Always`、`ValueIncrease`、`ValueDecrease`、`RisingEdge`、`FallingEdge`。
    - **Register**：触发寄存器地址。
    - **DataType**：触发寄存器数据类型。
  - **BatchReadRegister**：批量读取寄存器地址。
  - **BatchReadLength**：批量读取长度。
  - **TableName**：数据库表名。
  - **BatchSize**：批量保存大小，`1` 表示逐条保存。
  - **DataPoints**：数据配置。
    - **ColumnName**：数据库列名。
    - **Index**：寄存器索引。
    - **StringByteLength**：字符串字节长度。
    - **Encoding**：编码方式，支持 `UTF8`、`GB2312`、`GBK`、`ASCII`。
    - **DataType**：寄存器数据类型。
    - **EvalExpression**：数值转换表达式，例如 `value / 1000.0`。

### 配置示例
`Configs/M01_Metrics.json` 示例展示了典型配置方式。

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
        "DataType": null,
        "PollInterval": 0
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

## 系统配置
在 `Startup.cs` 中注册 `IDataAcquisition` 实例以管理采集任务。

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

## API

### 获取 PLC 连接状态
- `GET /api/DataAcquisition/GetPlcConnectionStatus`

该接口返回各 PLC 连接状态的字典。

## 贡献
欢迎通过 Pull Request 提交改进。提交前请确保所有相关测试通过并避免引入破坏性修改。

## 许可
本项目采用 MIT 许可证，详情见 [LICENSE](LICENSE)。

