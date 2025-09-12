# 🛰️ PLC 数据采集系统

[![Stars](https://img.shields.io/github/stars/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/stargazers)
[![Forks](https://img.shields.io/github/forks/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/network/members)
[![License](https://img.shields.io/github/license/liuweichaox/DataAcquisition.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](#)

**中文 | [English](README.en.md)**

## 📙 概述

PLC 数据采集系统用于从可编程逻辑控制器（PLC）实时采集运行数据，并将结果写入**消息队列**与**数据库**，以支撑工业设备**在线监控、性能分析与故障诊断**。系统基于 .NET 8.0，跨平台运行于 Windows、Linux 与 macOS。

## 💡 核心功能

- **高效通讯**：基于 Modbus TCP（示例）实现稳定读写，可扩展其它协议。
- **多 PLC 采集**：支持并行/周期性读取多个 PLC。
- **频率控制**：采集频率可配置，最低支持毫秒级。
- **数据预处理**：写入前支持表达式转换与过滤。
- **错误处理**：断线重连、超时重试。
- **消息队列**：对接 RabbitMQ、Kafka 或本地队列，缓冲高并发写入。
- **数据存储**：支持 SQLite 以及多种云端数据库。
- **日志记录**：可自定义日志策略，便于审计与排障。
- **动态配置**：通过 JSON/数据库定义表结构、列名、采集频率与触发规则。
- **多平台支持**：.NET 8.0，Windows/Linux/macOS。

## 🏗️ 架构总览

- **DataAcquisition.Domain**：领域模型与枚举
- **DataAcquisition.Application**：接口与服务契约
- **DataAcquisition.Infrastructure**：默认实现
- **DataAcquisition.Gateway**：基于 HslCommunication 的参考实现（可作为扩展样例）

### 🧰 可扩展接口（自定义实现）

- `IOperationalEventsService`：运行事件与日志记录
- `IDeviceConfigService`：设备配置加载（JSON/DB/其它来源）
- `IPlcClientService`：PLC 底层通讯
- `IPlcClientFactory`：自定义 PLC 客户端工厂
- `IDataProcessingService`：采集结果预处理
- `IDataStorageService`：数据写入数据库
- `IQueueService`：推送数据到消息队列

**集成步骤**

1. 在 `Program.cs` 注册你的自定义实现，替换默认依赖。
2. 构建并运行项目，按需调整配置。

## 🚀 快速开始

### 🌐 环境要求

- .NET 8.0 SDK
- 可选：RabbitMQ 或 Kafka（消息队列）
- 可选：SQLite 或其它数据库驱动

### ⬇️ 安装

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
dotnet restore
dotnet build
```

### ▶️ 运行

```bash
dotnet run --project DataAcquisition.Gateway
```

默认监听：`http://localhost:8000`

## 🗂️ 仓库结构

```text
DataAcquisition/
├── DataAcquisition.Application/      # 接口与服务契约
│   └── Abstractions/                 # 核心接口定义
├── DataAcquisition.Domain/           # 领域模型与枚举
│   ├── Clients/                      # PLC 客户端模型
│   ├── Models/                       # 通用领域实体
│   └── OperationalEvents/            # 运行事件模型
├── DataAcquisition.Infrastructure/   # 默认接口实现
│   ├── Clients/                      # PLC 客户端实现
│   ├── DataAcquisitions/             # 采集流程服务
│   ├── DataProcessing/               # 数据预处理实现
│   ├── DataStorages/                 # 数据存储实现
│   ├── DeviceConfigs/                # 设备配置加载
│   ├── OperationalEvents/            # 运行事件处理
│   └── Queues/                       # 消息队列实现
├── DataAcquisition.Gateway/          # 网关层示例
│   ├── BackgroundServices/           # 后台任务
│   ├── Configs/                      # 采集配置文件
│   ├── Controllers/                  # API 控制器
│   ├── Hubs/                         # SignalR Hub
│   ├── Models/                       # Web 层模型
│   ├── Views/                        # Razor 视图
│   └── wwwroot/                      # 静态资源
├── DataAcquisition.sln
├── README.md
└── README.en.md
```

## 📝 配置

`DataAcquisition.Gateway/Configs` 存放各 PLC/模块的 JSON 配置，定义 IP、寄存器、数据类型、触发与目标表等。默认从 JSON 加载；若需改为数据库等来源，实现 `IDeviceConfigService` 即可。

### 📐 配置结构（示意，以 YAML 说明）

```yaml
# 仅为结构说明（示意）
IsEnabled: true
Code: string # PLC 编码
Host: string # PLC IP
Port: number # 通讯端口
Type: Mitsubishi|Inovance|BeckhoffAds
HeartbeatMonitorRegister: string # [可选] 心跳寄存器
HeartbeatPollingInterval: number # [可选] 心跳轮询间隔(ms)
Channels: # 采集通道列表，每个通道都是独立采集任务
  - ChannelName: string # 通道名称
    Lifecycle: # [可选] 采集开始/结束触发器
      Register: string # [可选] 触发地址
      DataType: ushort|uint|ulong|short|int|long|float|double # [可选]
      Start:
        TriggerMode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge
        Operation: Insert|Update
        StampColumn: string # [可选] 开始时间列名
      End:
        TriggerMode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge
        Operation: Insert|Update
        StampColumn: string # [可选] 结束时间列名
    EnableBatchRead: bool
    BatchReadRegister: string
    BatchReadLength: int
    TableName: string
    BatchSize: int # 1 表示逐条保存
    DataPoints:
      - ColumnName: string
        Register: string
        Index: int
        StringByteLength: int
        Encoding: UTF8|GB2312|GBK|ASCII
        DataType: ushort|uint|ulong|short|int|long|float|double|string|bool
        EvalExpression: string # 使用变量 value 表示原始值
```

### 🔢 枚举说明

- **Type**
  - `Mitsubishi`：三菱 PLC
  - `Inovance`：汇川 PLC
  - `BeckhoffAds`：倍福 ADS

- **Lifecycle.Start.TriggerMode / Lifecycle.End.TriggerMode**
  - `Always`：无条件采集
  - `ValueIncrease`：寄存器值增加时采集
  - `ValueDecrease`：寄存器值减少时采集
  - `RisingEdge`：寄存器从 0 变 1 触发
  - `FallingEdge`：寄存器从 1 变 0 触发

- **Lifecycle.DataType / DataPoints.DataType**
  - `ushort`、`uint`、`ulong`、`short`、`int`、`long`、`float`、`double`
  - `string`（仅 DataPoints）
  - `bool`（仅 DataPoints）

- **Encoding**
  - `UTF8`、`GB2312`、`GBK`、`ASCII`

- **Lifecycle.Start.Operation / Lifecycle.End.Operation**
  - `Insert`（插入）
  - `Update`（更新）

- **Lifecycle.Start.StampColumn / Lifecycle.End.StampColumn**
  - 记录开始或结束时间的列名。

### 🧮 EvalExpression 用法

在写入数据库前对读数做表达式转换。表达式中使用变量 `value` 代表原始值，例如：`"value / 1000.0"`。空字符串表示不转换。

### 📘 配置示例

`DataAcquisition.Gateway/Configs/M01C123.json`：

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
        "Register": "D6200",
        "DataType": "short",
        "Start": {
          "TriggerMode": "RisingEdge",
          "Operation": "Insert",
          "StampColumn": "start_time"
        },
        "End": {
          "TriggerMode": "FallingEdge",
          "Operation": "Update",
          "StampColumn": "end_time"
        }
      }
    }
  ]
}
```

## 🔗 API

### 获取 PLC 连接状态

- `GET /api/DataAcquisition/GetPlcConnectionStatus`
  返回各 PLC 的连接状态字典。

### 写入 PLC 寄存器

- `POST /api/DataAcquisition/WriteRegister`
  请求示例（支持批量写入，`dataType` 指定值类型）：

```json
{
  "plcCode": "PLC01",
  "items": [
    { "address": "D100", "dataType": "short", "value": 1 },
    { "address": "D101", "dataType": "int", "value": 2 }
  ]
}
```

## 📦 依赖（NuGet）

### 基础框架

- Microsoft.Extensions.Caching.Memory `9.0.2`
- NCalcAsync `5.4.0`
- Newtonsoft.Json `13.0.3`

### 示例实现

- Dapper `2.1.66`
- HslCommunication `12.2.0`
- MySqlConnector `2.4.0`
- Microsoft.AspNetCore.SignalR `1.2.0`
- Serilog.AspNetCore `9.0.0`
- Serilog.Sinks.Console `6.0.0`
- Serilog.Sinks.File `7.0.0`

## 💻 开发与注册

在 `Program.cs` 中注册服务：

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

## 🚢 部署

使用自包含发布生成跨平台可执行文件：

```bash
dotnet publish DataAcquisition.Gateway -c Release -r win-x64  --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r linux-x64 --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r osx-x64  --self-contained true
```

将 `publish` 目录内容复制到目标环境并运行相应平台的可执行文件。

## 🙏 贡献

欢迎提交 PR。请确保测试通过并避免引入破坏性修改。

## 📜 许可

本项目使用 MIT 许可证，详情见 [LICENSE](LICENSE)。
