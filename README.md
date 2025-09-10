# 🛰️ PLC 数据采集系统

[![Stars](https://img.shields.io/github/stars/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/stargazers)
[![Forks](https://img.shields.io/github/forks/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/network/members)
[![License](https://img.shields.io/github/license/liuweichaox/DataAcquisition.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](#)

**中文** | [English](README.en.md)

## 📙 概述
PLC 数据采集系统用于从可编程逻辑控制器实时收集运行数据，并将结果传递至消息队列和数据库，以支持工业设备监控、性能分析与故障诊断。

## 💡 核心功能
- 高效通讯：基于 Modbus TCP 协议实现稳定的数据传输。
- 消息队列：可将采集结果写入 RabbitMQ、Kafka 或本地队列以处理高并发。
- 数据存储：支持本地 SQLite 数据库及多种云端数据库。
- 日志记录：允许自定义日志策略，便于排查和审计。
- 多 PLC 数据采集：支持同时周期性读取多个 PLC。
- 错误处理：提供断线重连和超时重试机制。
- 数据预处理：在写入前转换和过滤采集数据。
- 频率控制：采集频率可配置，最低支持毫秒级。
- 动态配置：通过配置文件定义表结构、列名和频率。
- 多平台支持：基于 .NET 8.0，运行于 Windows、Linux 和 macOS。

## 🏗️ 架构概览
- **DataAcquisition.Domain**：领域模型与枚举。
- **DataAcquisition.Application**：接口与服务契约。
- **DataAcquisition.Infrastructure**：默认实现。
- **DataAcquisition.Gateway**：基于 HslCommunication 的参考实现，可作为自定义实现的示例。

### 🧰 如何自定义实现
1. 实现 `IPlcClientService` 与 `IPlcClientFactory`，以接入新的 PLC 协议或通讯方式。
2. 实现 `IDataStorageService` 以支持不同的数据库或持久化方案。
3. 实现 `IQueueService` 以扩展消息队列。
4. 实现 `IOperationalEventsService` 以记录错误、日志等运行事件。
5. 实现 `IDataProcessingService` 以进行数据预处理。
6. 在 `Program.cs` 中注册自定义实现，替换默认依赖。
7. 构建并运行项目，按需调整配置文件。

## 📦 NuGet 包
### 🧱 基础框架依赖
核心框架使用以下 NuGet 包：
- [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory) 9.0.2：提供内存缓存功能。
- [NCalcAsync](https://www.nuget.org/packages/NCalcAsync) 5.4.0：在数据写入前执行表达式计算。
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) 13.0.3：用于 JSON 序列化与反序列化。

### 🧪 示例依赖
参考实现（如 `DataAcquisition.Gateway`）使用以下 NuGet 包：
- [Dapper](https://www.nuget.org/packages/Dapper) 2.1.66：轻量级 ORM，用于数据访问。
- [HslCommunication](https://www.nuget.org/packages/HslCommunication) 12.2.0：支持多种 PLC 通讯协议。
- [Microsoft.AspNetCore.SignalR](https://www.nuget.org/packages/Microsoft.AspNetCore.SignalR) 1.2.0：实现实时 Web 通讯。
- [MySqlConnector](https://www.nuget.org/packages/MySqlConnector) 2.4.0：高性能 MySQL 客户端驱动。
- [Serilog.AspNetCore](https://www.nuget.org/packages/Serilog.AspNetCore) 9.0.0：集成 Serilog 日志框架。
- [Serilog.Sinks.Console](https://www.nuget.org/packages/Serilog.Sinks.Console) 6.0.0：将日志输出到控制台。
- [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File) 7.0.0：将日志写入文件。

## 🌐 环境要求
- .NET 8.0 SDK
- 可选：RabbitMQ 或 Kafka（用于消息队列）
- 可选：SQLite 或其他数据库驱动

## 🔧 安装
### ⬇️ 克隆仓库
```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
```
### 🔄 恢复依赖
```bash
dotnet restore
```

## 📝 配置
`DataAcquisition.Gateway/Configs` 目录包含与数据库表对应的 JSON 文件，每个文件定义 PLC 地址、寄存器、数据类型等信息，可根据实际需求调整。

### 📐 配置结构说明
配置文件使用 JSON 格式，结构如下（以 YAML 描述）：

```yaml
# 配置结构说明（仅用于展示）
IsEnabled: true                 # 是否启用
Code: string                    # PLC 编码
Host: string                    # PLC IP 地址
Port: number                    # PLC 通讯端口
Type: Mitsubishi|Inovance       # PLC 类型
HeartbeatMonitorRegister: string # [可选] 心跳监控寄存器地址
HeartbeatPollingInterval: number # [可选] 心跳轮询间隔（毫秒）
Modules:                        # 采集模块配置数组
  - ChamberCode: string         # 采集通道编码
    Trigger:                    # 触发配置
      Mode: Always|ValueIncrease|ValueDecrease|RisingEdge|FallingEdge # 触发模式
      Register: string          # 触发寄存器地址
      DataType: ushort|uint|ulong|short|int|long|float|double # 触发寄存器数据类型
      Operation: Insert|Update  # 数据操作类型
      TimeColumnName: string    # [可选] 时间列名
    BatchReadRegister: string   # 批量读取寄存器地址
    BatchReadLength: int        # 批量读取长度
    TableName: string           # 数据库表名
    BatchSize: int              # 批量保存大小，1 表示逐条保存
    DataPoints:                 # 数据配置
      - ColumnName: string      # 数据库列名
        Index: int              # 寄存器索引
        StringByteLength: int   # 字符串字节长度
        Encoding: UTF8|GB2312|GBK|ASCII # 编码方式
        DataType: ushort|uint|ulong|short|int|long|float|double|string|bool # 寄存器数据类型
        EvalExpression: string  # 数值转换表达式，使用变量 value 表示原始值
```

### 🔢 枚举值说明
- **Type**
  - `Mitsubishi`：三菱 PLC。
  - `Inovance`：汇川 PLC。
- **Trigger.Mode**
  - `Always`：始终采样。
  - `ValueIncrease`：寄存器值增加时采样。
  - `ValueDecrease`：寄存器值减少时采样。
  - `RisingEdge`：寄存器从 0 变为 1 时采样。
  - `FallingEdge`：寄存器从 1 变为 0 时采样。
- **Trigger.DataType / DataPoints.DataType**
  - `ushort`、`uint`、`ulong`。
  - `short`、`int`、`long`。
  - `float`、`double`。
  - `string`、`bool`（仅用于 DataPoints）。
- **Encoding**
  - `UTF8`、`GB2312`、`GBK`、`ASCII`。
- **Trigger.Operation**
  - `Insert`：插入新记录。
  - `Update`：更新已有记录。
- **Trigger.TimeColumnName**
  - 可选的时间列名。在 `Update` 操作时，该列写入结束时间，匹配的 `Insert` 操作的时间列用于定位记录。

### 🧮 EvalExpression 用法
`EvalExpression` 用于在写入数据库前对寄存器读数进行转换。表达式中可使用变量 `value` 表示原始值，如 `"value / 1000.0"`。留空字符串则不进行任何转换。

### 🗒️ 配置示例
`DataAcquisition.Gateway/Configs/M01C123.json` 展示了典型配置：

```json
{
  "IsEnabled": true,
  "Code": "M01C123",
  "Host": "192.168.1.110",
  "Port": 4104,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D6061",
  "Modules": [
    {
      "ChamberCode": "M01C01",
      "Trigger": {
        "Mode": "Always",
        "Register": "D6000",
        "DataType": "short",
        "Operation": "Insert",
        "TimeColumnName": ""
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

## ▶️ 运行
确保已安装 .NET 8.0 SDK。

```bash
dotnet build
dotnet run --project DataAcquisition.Gateway
```

服务启动后默认监听 http://localhost:8000 端口。

## 💻 开发
### 🔧 系统配置
在 `Program.cs` 中注册 `IDataAcquisitionService` 实例以管理采集任务。

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

### 🗂️ 仓库结构
- `DataAcquisition.Domain`：领域模型与枚举。
- `DataAcquisition.Application`：接口与服务契约。
- `DataAcquisition.Infrastructure`：接口实现。
- `DataAcquisition.Gateway`：示例网关层。

### 🔨 构建
```bash
dotnet build
```

## 🔗 API
### 📶 获取 PLC 连接状态
- `GET /api/DataAcquisition/GetPlcConnectionStatus`

该接口返回各 PLC 连接状态的字典。

### ✏️ 写入 PLC 寄存器
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

## 🚢 部署
使用 `dotnet publish` 生成跨平台的自包含可执行文件：

```bash
dotnet publish DataAcquisition.Gateway -c Release -r win-x64 --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r linux-x64 --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r osx-x64 --self-contained true
```

将生成的 `publish` 目录内容复制到目标环境并运行对应平台的可执行文件。

## 🙏 贡献
欢迎通过 Pull Request 提交改进。提交前请确保所有相关测试通过并避免引入破坏性修改。

## 📜 许可
本项目采用 MIT 许可证，详情见 [LICENSE](LICENSE)。

