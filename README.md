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
- **数据存储**：基于 InfluxDB 时序数据库，专为高频时序数据采集优化。
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
    ConditionalAcquisition: # [可选] 条件采集配置，null 表示无条件采集
      Register: string # [可选] 触发地址
      DataType: ushort|uint|ulong|short|int|long|float|double # [可选]
      Start:
        TriggerMode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge
        Operation: Insert
        StampColumn: string # [可选] 开始时间列名
      End:
        TriggerMode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge
        Operation: Insert
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

- **ConditionalAcquisition.Start.TriggerMode / ConditionalAcquisition.End.TriggerMode**

  - `Always`：无条件采集
  - `ValueIncrease`：寄存器值增加时采集
  - `ValueDecrease`：寄存器值减少时采集
  - `RisingEdge`：寄存器从 0 变 1 触发
  - `FallingEdge`：寄存器从 1 变 0 触发

- **ConditionalAcquisition.DataType / DataPoints.DataType**

  - `ushort`、`uint`、`ulong`、`short`、`int`、`long`、`float`、`double`
  - `string`（仅 DataPoints）
  - `bool`（仅 DataPoints）

- **Encoding**

  - `UTF8`、`GB2312`、`GBK`、`ASCII`

- **ConditionalAcquisition.Start.Operation / ConditionalAcquisition.End.Operation**

  - `Insert`（插入，时序数据库统一使用 Insert 操作，End 事件通过 event_type 标签区分）

- **ConditionalAcquisition.Start.StampColumn / ConditionalAcquisition.End.StampColumn**
  - 记录开始或结束时间的列名。

### 🔄 条件采集与 CycleId 机制

当配置了 `ConditionalAcquisition` 时，系统会进行**条件采集**，根据 PLC 寄存器状态判断何时开始和结束采集。

**注意**：所有采集（包括无条件采集）都会生成 `cycle_id`，便于数据追踪和管理。

#### 工作原理

1. **开始事件（Start）**：

   - 当满足 Start 触发条件时（如 RisingEdge：从 0 变 1），系统会：
     - 生成唯一的 `cycle_id`（GUID 格式）
     - 插入新记录，包含所有数据点和 `cycle_id`、开始时间
     - 在内存中保存该采集周期的状态

2. **结束事件（End）**：
   - 当满足 End 触发条件时（如 FallingEdge：从 1 变 0），系统会：
     - 从内存中获取对应的 `cycle_id`
     - **写入新的数据点**，使用`event_type="end"`标签标识
     - 通过`cycle_id`标签关联 Start 事件
     - 如果找不到对应的 cycle（异常情况），会记录错误日志并跳过

#### 优势

- **时序数据库特性**：符合时序数据库设计，所有事件作为独立数据点存储，保留完整历史
- **精确匹配**：每个采集周期都有唯一标识（cycle_id），确保 Start 和 End 正确关联
- **易于追踪**：可以通过 `cycle_id` 标签查询完整的采集周期
- **高性能写入**：InfluxDB 针对高频时序数据写入优化，支持批量写入

#### InfluxDB 数据结构

所有采集数据写入 InfluxDB，使用以下结构：

**数据点结构**：

- **Measurement**：表名（TableName）
- **Tags**（标签，用于查询和分组）：
  - `device_code`：设备编码
  - `channel_name`：通道名称
  - `cycle_id`：采集周期唯一标识（GUID）
  - `event_type`：事件类型（"start" | "end" | "data"）
- **Fields**（字段，存储数值）：
  - 所有采集的数据点值
  - 时间戳字段（如 start_time、end_time）
- **Timestamp**：采集时间

**示例（InfluxDB Line Protocol）**：

```
measurement,device_code=PLC01,channel_name=Channel1,cycle_id=xxx,event_type=start field1=value1,field2=value2 1234567890000000000
measurement,device_code=PLC01,channel_name=Channel1,cycle_id=xxx,event_type=end end_time=1234567890000000000 1234567891000000000
```

**查询示例**：

- 查询特定 cycle_id 的所有事件：`from(bucket: "plc_data") |> filter(fn: (r) => r["cycle_id"] == "xxx")`
- 查询 Start 事件：`from(bucket: "plc_data") |> filter(fn: (r) => r["event_type"] == "start")`

#### 典型应用场景

1. **生产周期管理**

   - 场景：生产线开始生产时记录开始时间，生产结束时更新结束时间
   - 配置：Start 使用 RisingEdge（生产开始信号从 0 变 1），End 使用 FallingEdge（生产结束信号从 1 变 0）
   - 数据：记录生产开始时间、结束时间、产量、质量等数据

2. **设备运行状态监控**

   - 场景：设备启动时记录运行开始时间，设备停止时更新停止时间
   - 配置：Start 使用 RisingEdge（运行信号从 0 变 1），End 使用 FallingEdge（运行信号从 1 变 0）
   - 数据：记录设备运行时长、能耗、故障次数等

3. **批次管理**

   - 场景：批次开始插入记录，批次结束更新记录
   - 配置：Start 使用 ValueIncrease（批次号增加），End 使用 ValueDecrease（批次号减少）
   - 数据：记录批次号、开始时间、结束时间、批次产量等

4. **工艺参数采集**

   - 场景：工艺开始时采集初始参数，工艺结束时采集最终参数
   - 配置：Start 使用 RisingEdge（工艺启动信号），End 使用 FallingEdge（工艺结束信号）
   - 数据：记录温度、压力、速度等工艺参数的变化

5. **质量检测周期**
   - 场景：检测开始时记录检测参数，检测结束时更新检测结果
   - 配置：Start 使用 RisingEdge（检测开始信号），End 使用 FallingEdge（检测结束信号）
   - 数据：记录检测时间、检测结果、合格率等

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
      "ChannelName": "M01C01",
      "TableName": "m01c01_sensor",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 70,
      "BatchSize": 1,
      "DataPoints": [
        {
          "ColumnName": "up_temp",
          "Register": "D6002",
          "Index": 2,
          "DataType": "short"
        },
        {
          "ColumnName": "down_temp",
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
      "TableName": "m01c01_recipe",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6100",
      "BatchReadLength": 200,
      "BatchSize": 1,
      "DataPoints": [
        {
          "ColumnName": "up_set_temp",
          "Register": "D6102",
          "Index": 2,
          "DataType": "short"
        },
        {
          "ColumnName": "down_set_temp",
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
          "Operation": "Insert",
          "StampColumn": "start_time"
        },
        "End": {
          "TriggerMode": "FallingEdge",
          "Operation": "Insert",
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

- InfluxDB.Client `2.0.0`
- HslCommunication `12.2.0`
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
builder.Services.AddSingleton<IDataStorageService, InfluxDbDataStorageService>();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();
builder.Services.AddSingleton<IPlcStateManager, PlcStateManager>();
builder.Services.AddSingleton<IAcquisitionStateManager, AcquisitionStateManager>();  // 采集周期状态管理
builder.Services.AddSingleton<ITriggerEvaluator, TriggerEvaluator>();                 // 触发条件评估
builder.Services.AddSingleton<IChannelCollector, ChannelCollector>();
builder.Services.AddSingleton<IDataAcquisitionService, DataAcquisitionService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
builder.Services.AddHostedService<QueueHostedService>();
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
