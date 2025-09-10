# 🛰️ PLC 数据采集系统

[![Stars](https://img.shields.io/github/stars/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/stargazers)
[![Forks](https://img.shields.io/github/forks/liuweichaox/DataAcquisition?style=social)](https://github.com/liuweichaox/DataAcquisition/network/members)
[![License](https://img.shields.io/github/license/liuweichaox/DataAcquisition.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](#)

**中文** | [English](README.en.md)

## 📙 概述
PLC 数据采集系统用于从可编程逻辑控制器实时收集运行数据，并将结果传递至消息队列和数据库，以支持设备监控、性能分析与故障诊断。

## 💡 核心功能
- 基于 Modbus TCP 协议的高效通讯
- 将采集结果写入 RabbitMQ、Kafka 或本地队列
- 支持 SQLite 及多种云端数据库
- 可自定义的日志策略，便于排查与审计
- 支持同时周期性采集多个 PLC
- 断线重连与超时重试机制
- 数据预处理后再持久化
- 采集频率可配置，最低毫秒级
- 可通过 JSON 文件或数据库动态配置表结构、列名与采样频率
- 基于 .NET 8.0，兼容 Windows、Linux 与 macOS

## 🏗️ 架构
- **DataAcquisition.Domain** —— 领域模型与枚举
- **DataAcquisition.Application** —— 接口与服务契约
- **DataAcquisition.Infrastructure** —— 默认实现
- **DataAcquisition.Gateway** —— 基于 HslCommunication 的参考网关

## 📦 依赖
### 框架依赖
- [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory) 9.0.2
- [NCalcAsync](https://www.nuget.org/packages/NCalcAsync) 5.4.0
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) 13.0.3

### 示例依赖
- [Dapper](https://www.nuget.org/packages/Dapper) 2.1.66
- [HslCommunication](https://www.nuget.org/packages/HslCommunication) 12.2.0
- [MySqlConnector](https://www.nuget.org/packages/MySqlConnector) 2.4.0
- [Microsoft.AspNetCore.SignalR](https://www.nuget.org/packages/Microsoft.AspNetCore.SignalR) 1.2.0
- [Serilog.AspNetCore](https://www.nuget.org/packages/Serilog.AspNetCore) 9.0.0
- [Serilog.Sinks.Console](https://www.nuget.org/packages/Serilog.Sinks.Console) 6.0.0
- [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File) 7.0.0

## 🔧 安装
### 环境要求
- .NET 8.0 SDK
- 可选：RabbitMQ 或 Kafka
- 可选：SQLite 或其他数据库驱动

### 克隆并恢复
```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
dotnet restore
```

## ⚙️ 配置
- 默认设备配置存放在 `DataAcquisition.Gateway/Configs` 目录下的 JSON 文件中
- 若需从数据库加载配置，可实现 `IDeviceConfigService`

示例 JSON：
```json
{
  "Code": "M01C123",
  "Host": "192.168.1.110",
  "Port": 4104
}
```

## ▶️ 使用
构建并运行网关项目：
```bash
dotnet build
dotnet run --project DataAcquisition.Gateway
```
服务默认监听 `http://localhost:8000` 端口。

## 🔗 API
### 获取 PLC 连接状态
`GET /api/DataAcquisition/GetPlcConnectionStatus`

### 写入 PLC 寄存器
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

## 💻 开发
在 `Program.cs` 中注册服务以管理采集任务：
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

## 🙏 贡献
欢迎通过 Pull Request 提交改进。提交前请确保相关测试通过，并避免引入破坏性修改。

## 📜 许可
本项目基于 MIT 许可证发布，详见 [LICENSE](LICENSE)。
