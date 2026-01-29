# 🛰️ DataAcquisition - 工业级 PLC 数据采集系统

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Version](https://img.shields.io/badge/version-1.0.0-blue)]()
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)]()

English: [README.en.md](README.en.md)

## 📋 目录

- [📖 项目简介](#-项目简介)
- [🎯 核心特性](#-核心特性)
- [✨ 应用场景](#-应用场景)
- [🏗️ 系统架构](#-系统架构)
- [📁 项目结构](#-项目结构)
- [🚀 快速开始](#-快速开始)
- [📸 屏幕截图](#-屏幕截图)
- [📚 教程导航](#-教程导航)
- [📖 文档导航](#-文档导航)
- [🤝 贡献指南](#-贡献指南)
- [📄 开源协议](#-开源协议)
- [🙏 致谢](#-致谢)

## 📖 项目简介

DataAcquisition 是一个基于 .NET 构建的工业级 PLC 数据采集系统。系统采用 **WAL-first（写前日志）架构**确保数据零丢失，支持 **Edge-Central 分布式架构**实现多车间集中管理。提供多 PLC 并行采集、条件触发采集、批量读取优化等高级功能，支持配置热更新和实时监控，开箱即用，运维友好。

**技术栈：**
- 运行时：.NET 10.0
- 数据存储：InfluxDB 2.x（时序数据库）+ Parquet（本地 WAL）
- 监控：Prometheus 指标 + Vue3 可视化界面
- 架构：Edge-Central 分布式架构

### 🎯 核心特性

#### 🔒 WAL-first 数据安全架构

系统采用 **Write-Ahead Log (WAL) 优先** 的设计理念，确保工业数据零丢失：

```
数据采集 → Parquet WAL (本地) → InfluxDB (远程)
              ↓ (失败保留)         ↓ (失败重试)
         pending/ 目录         retry/ 目录
```

- **双重保险**：数据同时写入本地 Parquet 文件和 InfluxDB，任一失败都有备份
- **自动重试**：后台 Worker 每 5 秒扫描 retry/ 目录，自动重传失败数据
- **故障恢复**：即使网络中断、数据库宕机，数据也不会丢失

#### ⚡ 高性能采集优化

| 特性 | 说明 | 性能提升 |
|------|------|---------|
| **批量读取** | 一次性读取连续寄存器块，减少网络往返 | ~10x 速度提升 |
| **并行采集** | 多 PLC、多通道同时采集 | 支持 100+ 设备并发 |
| **条件触发** | 仅在关键事件发生时采集，节省资源 | 减少 80% 无效采集 |
| **智能聚合** | 按 BatchSize 聚合后批量写入 | 减少数据库压力 |

#### 🎯 智能采集模式

**Always 模式**（持续采集）
- 适用场景：温度、压力、电流等需要持续监控的参数
- 按固定间隔采集数据

**Conditional 模式**（条件触发采集）
- 适用场景：生产周期管理、设备状态变化记录
- 支持 RisingEdge（上升沿触发）和 FallingEdge（下降沿触发）
- 自动记录 Start/End 事件，通过 CycleId 关联完整生产周期

#### 🌐 Edge-Central 分布式架构

- **Edge Agent**：部署在车间侧，负责 PLC 数据采集和本地存储
- **Central API**：中心服务，接收边缘节点注册、心跳和数据上报
- **Central Web**：Vue3 可视化界面，实时展示系统状态和监控指标

#### 🔄 配置热更新

- 修改配置文件后自动重新加载（默认延迟 500ms）
- 支持设备配置和应用配置热更新
- 无需重启服务，不影响生产环境运行

#### 📊 完整的监控体系

- **Prometheus 指标**：采集延迟、队列深度、写入延迟、错误统计等
- **可视化界面**：Vue3 + Element Plus，实时展示边缘节点列表和系统指标
- **日志查询**：SQLite 日志存储，支持 API 查询和分页

#### 🔀 多协议支持

- Mitsubishi（三菱 PLC）
- Inovance（汇川 PLC）
- BeckhoffAds（倍福 PLC）
- 支持通过实现 `IPlcClientService` 接口扩展新协议

## ✨ 应用场景

### 📦 制造业生产线数据采集

**场景**：某汽车零部件生产线，需要实时采集 50+ 工位的设备状态、工艺参数和质量数据

**解决方案**：
- 每个工位部署 Edge Agent 采集 PLC 数据
- 使用条件触发模式记录每个产品的完整生产过程
- 通过 CycleId 关联产品从上料到下料的全部数据
- Central Web 实时监控各工位状态和产量统计

**效果**：
- ✅ 数据零丢失，满足质量追溯要求
- ✅ 条件触发采集，节省 80% 存储空间
- ✅ 批量读取优化，采集延迟 < 100ms

### 🏭 多车间集中监控

**场景**：某制造企业有 5 个车间分布在不同地区，需要集中监控设备运行状态

**解决方案**：
- 每个车间部署 Edge Agent 采集本地设备数据
- 所有 Edge Agent 向同一个 Central API 注册和上报心跳
- Central Web 统一展示所有车间的设备状态和告警信息
- 使用 Grafana 展示跨车间的生产统计和趋势分析

**效果**：
- ✅ 分布式部署，单点故障不影响其他车间
- ✅ 集中管理，降低运维成本
- ✅ 实时监控，快速定位问题

### 🔧 设备预测性维护

**场景**：某化工企业需要监控关键设备（压缩机、泵）的振动、温度、压力等参数，预测设备故障

**解决方案**：
- 配置 Always 模式持续采集振动、温度、压力等参数
- 数据存储到 InfluxDB，保留 1 年历史数据
- 使用 Grafana 配置告警规则（超过阈值自动告警）
- 通过 Flux 查询分析历史趋势，建立预测模型

**效果**：
- ✅ 实时监控设备健康状态
- ✅ 提前 7-14 天预测设备故障
- ✅ 减少计划外停机 60%

### 📊 生产数据追溯

**场景**：某食品企业需要记录每批次产品的完整生产参数，满足质量追溯要求

**解决方案**：
- 使用条件触发模式，在批次开始时触发 Start 事件
- 记录生产过程中的所有关键参数（温度、时间、添加量等）
- 批次结束时触发 End 事件
- 通过 CycleId 查询某批次产品的完整生产记录

**效果**：
- ✅ 完整记录每批次生产数据
- ✅ 快速定位质量问题根因
- ✅ 满足食品安全追溯要求

## 🏗️ 系统架构

### 分布式架构概览

系统采用 **Edge-Central（边缘-中心）分布式架构**，支持多车间、多节点的集中式管理和监控：

```
                    ┌─────────────────────────────────────────┐
                    │           Central Web (Vue3)            │
                    │            可视化界面 / 监控面板           │
                    └───────────────────┬─────────────────────┘
                                        │ HTTP/API
                    ┌───────────────────▼─────────────────────┐
                    │         Central API                     │
                    │  • 边缘节点注册/心跳管理                   │
                    │  • 遥测数据接入                           │
                    │  • 查询与管理接口                         │
                    │  • Prometheus 指标聚合                   │
                    └───────┬─────────────────────┬───────────┘
                            │                     │
              ┌─────────────┘                     └───────────┐
              │                                               │
    ┌─────────▼─────────┐                          ┌──────────▼────────┐
    │   Edge Agent #1   │                          │   Edge Agent #N   │
    │      (Node 1)     │                          │      (Node N)     │
    └─────────┬─────────┘                          └─────────┬─────────┘
              │                                              │
              └──────────────────────────────────────────────┘
```

### Edge Agent 内部架构

每个 Edge Agent 采用分层架构设计，各层职责清晰，确保数据零丢失：

```
┌────────────────────────────┐        ┌──────────────────────────┐
│        PLC Device          │──────▶ │  Heartbeat Monitor Layer │
└────────────────────────────┘        └──────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│   Data Acquisition Layer   │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│    Queue Service Layer     │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│          Storage Layer     │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐        ┌──────────────────────────────┐
│      WAL Persistence       │──────▶ │ Time-Series Database Storage │
└────────────────────────────┘        └──────────────────────────────┘
                 │                                 │
                 ▼                                 │  Write Failed
┌────────────────────────────┐                     │
│      Retry Worker          │◀────────────────────┘
└────────────────────────────┘
```

### 核心数据流

#### Edge Agent 内部流程

1. **数据采集阶段**：PLC 设备 → `ChannelCollector`（支持条件触发、批量读取优化）
2. **数据聚合阶段**：`LocalQueueService` 按配置的 `BatchSize` 批量聚合数据
3. **数据持久化阶段**：
   - **Parquet WAL**：立即写入本地 Parquet 文件（写前日志，确保零丢失）
   - **InfluxDB**：同步写入时序数据库（主存储）
4. **容错处理阶段**：写入成功后删除 WAL 文件；写入失败时保留 WAL 文件，由 `RetryWorker` 定期重试
5. **数据上报阶段**：可选地将数据上报到 Central API（用于集中式管理和监控）

#### Edge-Central 交互流程

1. **节点注册阶段**：Edge Agent 启动时自动向 Central API 注册节点信息（EdgeId、AgentBaseUrl、Hostname）
2. **心跳上报阶段**：周期性发送心跳信息（默认间隔 10 秒），包含队列积压量、错误信息等状态
3. **遥测数据上报阶段**：批量上报采集的时序数据到 Central API（可选功能）
4. **监控查询阶段**：Central Web 前端通过 Central API 查询各边缘节点的状态、指标和日志

## 📁 项目结构

```
DataAcquisition/
├── src/DataAcquisition.Application/     # 应用层 - 接口定义
│   ├── Abstractions/               # 核心接口抽象
│   └── PlcRuntime.cs              # PLC 运行时
├── src/DataAcquisition.Contracts/       # 契约层 - 对外 DTO/协议模型
├── src/DataAcquisition.Domain/         # 领域层 - 核心模型
│   └── Models/                     # 数据模型
├── src/DataAcquisition.Infrastructure/ # 基础设施层 - 实现
│   ├── Clients/                    # PLC 客户端实现
│   ├── DataAcquisitions/           # 数据采集服务
│   ├── DataStorages/               # 数据存储服务
│   └── Metrics/                    # 指标收集
├── src/DataAcquisition.Edge.Agent/ # Edge Agent - 车间侧采集后台 + 指标 + 本地 API
│   ├── Configs/                    # 设备配置文件
│   └── Controllers/                # 管理 API 控制器
├── src/DataAcquisition.Central.Api/ # Central API - 中心侧 API（边缘注册/心跳/数据接入、查询与管理）
├── src/DataAcquisition.Central.Web/ # Central Web - 纯前端（Vue CLI / Vue3），通过 /api 访问 Central API
├── src/DataAcquisition.Simulator/      # PLC 模拟器 - 用于测试
│   ├── Simulator.cs               # 模拟器核心逻辑
│   ├── Program.cs                 # 程序入口
│   └── README.md                  # 模拟器文档
└── DataAcquisition.sln             # 解决方案文件
```

## 🚀 快速开始

### 方式一：本地部署（推荐新手）

请查看 [入门教程](docs/tutorial-getting-started.md)，该指南提供了从零开始的完整步骤，包括：

- 环境要求和安装步骤
- InfluxDB 本地安装或 Docker 部署
- 设备配置文件创建
- 系统启动和验证
- 使用 PLC 模拟器进行测试

### 方式二：Docker 快速启动（推荐测试）

使用 Docker Compose 快速部署 InfluxDB，无需手动安装数据库：

```bash
# 启动 InfluxDB
docker-compose up -d influxdb

# 初始化（访问 http://localhost:8086）
# 用户名：admin，密码：admin123

# 更新 appsettings.json 中的 Token

# 启动 Edge Agent
dotnet run --project src/DataAcquisition.Edge.Agent
```

详细说明见：[Docker InfluxDB 部署指南](docs/docker-influxdb.md)

> **提示**: 如果你是第一次使用，建议按照 [入门教程](docs/tutorial-getting-started.md) 的步骤操作。如果你已经熟悉系统，可以直接查看 [配置教程](docs/tutorial-configuration.md) 和 [API 使用示例](docs/api-usage.md)。

### 🧪 使用 PLC 模拟器进行测试

项目提供了独立的 PLC 模拟器（`DataAcquisition.Simulator`），可以模拟三菱 PLC 的行为，用于测试数据采集功能，无需真实的 PLC 设备。

#### 启动模拟器

```bash
cd src/DataAcquisition.Simulator
dotnet run
```

#### 模拟器特性

- ✅ 模拟三菱 PLC（MelsecA1EServer）
- ✅ 自动更新心跳寄存器（D100）
- ✅ 模拟 7 个传感器指标（温度、压力、电流、电压、光栅位置、伺服速度、生产序号）
- ✅ 支持条件采集测试（生产序号触发）
- ✅ 交互式命令控制（set/get/info/exit）
- ✅ 实时数据显示

#### 快速测试流程

1. **启动模拟器**：

```bash
cd src/DataAcquisition.Simulator
dotnet run
```

2. **配置测试设备**：

   在 `src/DataAcquisition.Edge.Agent/Configs/` 目录创建 `TEST_PLC.json`（参考 `src/DataAcquisition.Simulator/README.md` 中的完整配置示例）

3. **启动采集系统**：

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
dotnet run --project src/DataAcquisition.Central.Api

cd src/DataAcquisition.Central.Web
npm install
npm run serve
```

4. **观察数据采集**：
   - 访问 http://localhost:3000 查看中心 UI（Edges/Metrics/Logs）
   - 访问 http://localhost:8000/metrics 查看中心 API 自身指标页面
   - 检查 InfluxDB 中的 `sensor` 和 `production` measurement

详细说明请参考：[src/DataAcquisition.Simulator/README.md](src/DataAcquisition.Simulator/README.md)

## 📸 屏幕截图

### Central Web 可视化界面

> **注意**：以下为界面示意图，实际界面请参考系统运行后的效果

**边缘节点列表**
![edges.png](images/edges.png)

**系统监控指标**
![metrics.png](images/metrics.png)

**日志列表**
![logs.png](images/logs.png)

### Prometheus 监控面板

访问 `http://localhost:5000/metrics` 可以看到 Prometheus 格式的指标：

```prometheus
# HELP data_acquisition_collection_latency_ms 数据采集延迟(ms)
# TYPE data_acquisition_collection_latency_ms gauge
data_acquisition_collection_latency_ms{device="PLC01",channel="PLC01C01"} 12.5

# HELP data_acquisition_queue_depth 队列深度
# TYPE data_acquisition_queue_depth gauge
data_acquisition_queue_depth{device="PLC01"} 45

# HELP data_acquisition_errors_total 错误总数
# TYPE data_acquisition_errors_total counter
data_acquisition_errors_total{device="PLC01",type="connection"} 0
```

### InfluxDB 数据查询

使用 Flux 查询某设备的温度数据：

```flux
from(bucket: "iot")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["device_code"] == "PLC01")
  |> filter(fn: (r) => r["_field"] == "temperature")
  |> yield(name: "temperature")
```

## 📚 教程导航

按“入门 → 配置 → 部署 → 查询 → 开发”的主线学习：

- [入门教程](docs/tutorial-getting-started.md)
- [配置教程](docs/tutorial-configuration.md)
- [部署教程](docs/tutorial-deployment.md)
- [数据查询教程](docs/tutorial-data-query.md)
- [开发扩展教程](docs/tutorial-development.md)

完整索引见：[文档索引](docs/index.md)

## 📖 文档导航

主入口与完整目录请使用：[文档索引](docs/index.md)

## ⚙️ 配置说明

详细的配置说明请参考：[配置教程](docs/tutorial-configuration.md)

### 快速参考

| 配置类型 | 位置 | 说明 |
|---------|------|------|
| 设备配置 | `src/DataAcquisition.Edge.Agent/Configs/*.json` | 每个 PLC 设备对应一个 JSON 配置文件 |
| Edge Agent 配置 | `src/DataAcquisition.Edge.Agent/appsettings.json` | 应用层配置（数据库、API 等） |
| 配置热更新 | 自动检测 | 支持配置文件修改后自动热加载，无需重启服务 |

**设备配置示例：**

```json
{
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "PLC01C01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 10,
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Always",
      "Metrics": [
        {
          "MetricName": "temperature",
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        }
      ]
    }
  ]
}
```


## 🤝 贡献指南

我们欢迎各种形式的贡献！请参考以下步骤：

1. Fork 本项目
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

### 开发环境设置

```bash
# 克隆项目
git clone https://github.com/liuweichaox/DataAcquisition.git

# 安装依赖
dotnet restore

# 运行测试
dotnet test

# 构建项目
dotnet build
```

### 代码规范

- 遵循 .NET 编码规范
- 使用有意义的命名
- 添加必要的 XML 注释
- 编写单元测试

## 📄 开源许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件。

## 🙏 致谢

感谢以下开源项目：

- [.NET](https://dotnet.microsoft.com/) - 强大的开发平台
- [InfluxDB](https://www.influxdata.com/) - 高性能时序数据库
- [Prometheus](https://prometheus.io/) - 监控系统
- [Vue.js](https://vuejs.org/) - 渐进式 JavaScript 框架

---

**如有问题或建议，请提交 [Issue](https://github.com/liuweichaox/DataAcquisition/issues) 或通过 Pull Request 贡献代码！**
