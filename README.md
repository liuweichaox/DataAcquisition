# DataAcquisition

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](https://dotnet.microsoft.com/)

English: [README.en.md](README.en.md)

DataAcquisition 是一个面向工业现场的开源 PLC 数据采集运行时，重点解决三件事：

- 稳定采集：多 PLC、多通道并行采集，支持持续采集与条件触发采集。
- 本地可恢复：采用 WAL-first 链路，先写本地 Parquet，再写主存储。
- 易扩展：PLC 驱动通过稳定的 `Driver` 名称配置，框架核心与具体通讯库解耦。

它更像一个可部署、可扩展的 edge runtime，而不是一个大而全的 MES/SCADA 平台。

## 项目范围

当前项目聚焦以下能力：

- 车间侧 Edge Agent 采集 PLC 数据
- InfluxDB 作为主时序存储，Parquet 作为本地 WAL
- 条件采集周期管理与恢复诊断
- 中心侧节点注册、心跳和诊断代理
- Prometheus 指标与 Web 可视化面板

当前项目不追求：

- 替代完整 SCADA / MES
- 在核心模型里抽象所有 PLC 底层细节
- 把所有驱动的全部私有参数都做成统一公共字段

## 架构概览

核心数据链路：

```text
PLC -> ChannelCollector -> QueueService -> Parquet WAL -> InfluxDB
                                   |               |
                                   |               -> retry/
                                   -> invalid/
```

部署拓扑：

```text
Central Web -> Central API -> Edge Agent -> PLC
```

设计重点：

- WAL-first：先保留本地可恢复副本，再尝试主存储
- UTC 时间语义：避免跨节点和时区歧义
- 正式事件与诊断事件分离：`Start/End/Data` 与恢复诊断分不同 measurement
- 驱动扩展点清晰：`IPlcDriverProvider` / `IPlcClientService`

更多设计说明见 [docs/design.md](docs/design.md) 和 [docs/data-flow.md](docs/data-flow.md)。

## 核心能力

- 多 PLC、多通道异步并行采集
- `Always` / `Conditional` 两种采集模式
- RisingEdge / FallingEdge 条件触发
- 批量读取连续寄存器块
- 热更新设备配置
- WAL `pending/`、`retry/`、`invalid/` 生命周期
- active cycle 本地持久化恢复
- 中心化节点注册、心跳、指标和日志代理

## 驱动模型

项目对 PLC 驱动的设计目标是“配置简单、扩展明确”。

默认方式：

- 使用 HslCommunication 作为默认通讯实现
- 通过稳定的 `Driver` 名称配置协议
- 例如：`melsec-a1e`、`melsec-mc`、`siemens-s7`

标准配置字段：

- `Driver`
- `Host`
- `Port`
- `ProtocolOptions`
- `Channels`

配置配套：

- JSON Schema： [schemas/device-config.schema.json](schemas/device-config.schema.json)
- 示例配置： [examples/device-configs](examples/device-configs)
- 离线校验：`dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs`
- 默认校验目录可通过 `Acquisition:DeviceConfigService:ConfigDirectory` 配置，也可用 `--config-dir` 临时覆盖

扩展方式：

- 框架核心只依赖 [IPlcDriverProvider](src/DataAcquisition.Application/Abstractions/IPlcDriverProvider.cs)
- 自定义驱动不需要修改采集主链路

驱动清单见 [docs/hsl-drivers.md](docs/hsl-drivers.md)。

## 仓库结构

- [src/DataAcquisition.Edge.Agent](src/DataAcquisition.Edge.Agent)  
  车间侧采集进程，项目主入口。

- [src/DataAcquisition.Infrastructure](src/DataAcquisition.Infrastructure)  
  PLC 驱动、采集编排、WAL、主存储、日志和指标实现。

- [src/DataAcquisition.Application](src/DataAcquisition.Application)  
  应用层抽象与运行时契约。

- [src/DataAcquisition.Domain](src/DataAcquisition.Domain)  
  领域模型和配置模型。

- [src/DataAcquisition.Central.Api](src/DataAcquisition.Central.Api)  
  中心服务，负责边缘节点注册、心跳和诊断代理。

- [src/DataAcquisition.Central.Web](src/DataAcquisition.Central.Web)  
  Vue3 管理界面。

- [tests/DataAcquisition.Core.Tests](tests/DataAcquisition.Core.Tests)  
  当前核心测试项目。

## 快速开始

前置要求：

- .NET 10 SDK
- InfluxDB 2.x
- Node.js 20+（仅 Central Web 需要）

1. 构建项目

```bash
dotnet build DataAcquisition.sln
```

2. 启动 InfluxDB（示例）

```bash
docker compose -f docker-compose.tsdb.yml up -d
```

3. 检查或修改设备配置

- 示例配置： [src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json](src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json)
- 应用配置： [src/DataAcquisition.Edge.Agent/appsettings.json](src/DataAcquisition.Edge.Agent/appsettings.json)

4. 启动 Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

只校验配置而不启动采集：

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

如果需要中心侧：

```bash
dotnet run --project src/DataAcquisition.Central.Api
```

## 文档

主入口：

- [docs/index.md](docs/index.md)

推荐阅读顺序：

- [入门教程](docs/tutorial-getting-started.md)
- [配置教程](docs/tutorial-configuration.md)
- [部署教程](docs/tutorial-deployment.md)
- [驱动清单](docs/hsl-drivers.md)
- [设计说明](docs/design.md)
- [开发扩展教程](docs/tutorial-development.md)

## 开发与验证

常用命令：

```bash
dotnet build DataAcquisition.sln --no-restore
dotnet test tests/DataAcquisition.Core.Tests/DataAcquisition.Core.Tests.csproj --no-build
```

如果你要扩展 PLC 驱动，先读：

- [CONTRIBUTING.md](CONTRIBUTING.md)
- [docs/tutorial-development.md](docs/tutorial-development.md)

## 许可证

本项目使用 [MIT License](LICENSE)。
