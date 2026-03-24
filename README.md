# DataAcquisition

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

English: [README.en.md](README.en.md)

DataAcquisition 是一个面向工业现场的开源 PLC 数据采集运行时。

它解决的核心问题很明确：

- 从 PLC 稳定读取数据
- 在边缘侧先落本地 WAL，再写主存储
- 用简单、明确的配置把采集任务跑起来

这个项目更像一个可部署的 edge runtime，而不是一个“什么都做一点”的 SCADA/MES 平台。

## 它适合做什么

- 车间侧 PLC 数据采集
- 边缘节点本地缓冲和失败重试
- 时序数据写入 InfluxDB
- 条件触发型周期采集
- 统一管理多台 PLC 的配置、采集和诊断

## 它不打算做什么

- 不替代完整 MES / SCADA
- 不强行抽象所有 PLC 私有差异
- 不把所有驱动细节塞进一个“万能配置模型”

## 架构

主链路：

```text
PLC -> Collector -> Queue -> Parquet WAL -> Primary Storage
                           |               |
                           |               -> retry/
                           -> invalid/
```

部署拓扑：

```text
Central Web -> Central API -> Edge Agent -> PLC
```

设计重点：

- `Edge First`：Edge Agent 是主产品，Central 是辅助控制面
- `WAL First`：先保留本地可恢复副本，再尝试主存储
- `Driver + Provider`：协议配置简单，驱动扩展边界明确
- `UTC`：采集时间统一为 UTC，避免跨节点歧义

## 快速开始

前置要求：

- .NET 10 SDK
- InfluxDB 2.x
- Docker（如果你用仓库里的 compose 启动 InfluxDB）

1. 构建项目

```bash
dotnet build DataAcquisition.sln
```

2. 启动 InfluxDB

```bash
docker compose -f docker-compose.tsdb.yml up -d
```

3. 检查配置

- 设备配置示例： [src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json](src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json)
- 示例配置目录： [examples/device-configs](examples/device-configs)
- JSON Schema： [schemas/device-config.schema.json](schemas/device-config.schema.json)

4. 离线校验配置

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

如需校验其他目录：

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs --config-dir ./examples/device-configs
```

5. 启动 Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

如果你想先做本地联调，可以再启动模拟器：

```bash
dotnet run --project src/DataAcquisition.Simulator
```

## 配置模型

设备配置使用 JSON。

最小结构：

```json
{
  "SchemaVersion": 1,
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Driver": "melsec-a1e",
  "Host": "127.0.0.1",
  "Port": 502,
  "ProtocolOptions": {
    "connect-timeout-ms": "5000",
    "receive-timeout-ms": "5000"
  },
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": []
}
```

配置原则：

- `Driver` 只接受完整稳定名称，不接受别名
- `Host` 支持 IP 或主机名
- `ProtocolOptions` 只允许当前驱动支持的键
- 配置目录默认来自 `Acquisition:DeviceConfigService:ConfigDirectory`

完整说明见 [docs/tutorial-configuration.md](docs/tutorial-configuration.md)。

## 驱动模型

默认驱动实现基于 HslCommunication，但框架核心不依赖 Hsl。

当前内置驱动通过稳定的 `Driver` 名称选择，例如：

- `melsec-a1e`
- `melsec-mc`
- `siemens-s7`
- `omron-fins`
- `inovance-tcp`
- `beckhoff-ads`

驱动目录和协议选项见 [docs/hsl-drivers.md](docs/hsl-drivers.md)。

如果你要扩展新的 PLC 驱动，入口是：

- [IPlcDriverProvider](src/DataAcquisition.Application/Abstractions/IPlcDriverProvider.cs)
- [IPlcClientService](src/DataAcquisition.Application/Abstractions/IPlcClientService.cs)
- [CONTRIBUTING.md](CONTRIBUTING.md)

## 文档

文档首页：

- [docs/index.md](docs/index.md)

推荐阅读顺序：

- [快速开始](docs/tutorial-getting-started.md)
- [配置说明](docs/tutorial-configuration.md)
- [驱动目录](docs/hsl-drivers.md)
- [部署说明](docs/tutorial-deployment.md)
- [设计说明](docs/design.md)
- [开发扩展](docs/tutorial-development.md)
- [常见问题](docs/faq.md)

## 仓库结构

- [src/DataAcquisition.Edge.Agent](src/DataAcquisition.Edge.Agent)
  Edge 运行时主入口
- [src/DataAcquisition.Infrastructure](src/DataAcquisition.Infrastructure)
  采集、驱动、WAL、存储、日志和指标实现
- [src/DataAcquisition.Application](src/DataAcquisition.Application)
  抽象接口和运行时契约
- [src/DataAcquisition.Domain](src/DataAcquisition.Domain)
  领域模型和配置模型
- [src/DataAcquisition.Central.Api](src/DataAcquisition.Central.Api)
  中心侧 API
- [src/DataAcquisition.Central.Web](src/DataAcquisition.Central.Web)
  中心侧 Web 界面
- [tests/DataAcquisition.Core.Tests](tests/DataAcquisition.Core.Tests)
  核心测试

## 开发

常用命令：

```bash
dotnet build DataAcquisition.sln --no-restore
dotnet test tests/DataAcquisition.Core.Tests/DataAcquisition.Core.Tests.csproj
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

## 许可证

本项目使用 [MIT License](LICENSE)。
