# 🛰️ DataAcquisition - 工业级 PLC 数据采集系统

[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%208.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](https://dotnet.microsoft.com/)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Version](https://img.shields.io/badge/version-1.0.0-blue)]()
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)]()

English: [README.en.md](README.en.md)

## 📋 目录

- [📖 项目简介](#-项目简介)
- [🎯 核心特性](#-核心特性)
- [🏗️ 系统架构](#️-系统架构)
- [📁 项目结构](#-项目结构)
- [🚀 快速开始](#-快速开始)
- [⚙️ 配置说明](#️-配置说明)
- [🔌 API 使用示例](#-api-使用示例)
- [📊 核心模块文档](#-核心模块文档)
- [🔄 数据处理流程](#-数据处理流程)
- [🎯 性能优化](#-性能优化)
- [❓ 常见问题](#-常见问题)
- [🏆 设计理念](#-设计理念)
- [🤝 贡献指南](#-贡献指南)
- [📄 开源协议](#-开源协议)
- [🙏 致谢](#-致谢)

## 📖 项目简介

DataAcquisition 是一个基于 .NET 构建的高性能、高可靠性的工业数据采集系统，专门为 PLC（可编程逻辑控制器）数据采集场景设计。系统支持 .NET 10.0 和 .NET 8.0 两个 LTS 版本，采用 WAL-first 架构，确保数据零丢失，支持多 PLC 并行采集、条件触发采集、批量读取等高级功能。

### 🎯 核心特性

- ✅ **WAL-first 架构** - 写前日志保证数据不丢失
- ✅ **多 PLC 并行采集** - 支持多种 PLC 协议（Modbus, Beckhoff ADS, Inovance, Mitsubishi, Siemens）
- ✅ **条件触发采集** - 支持边沿触发、值变化触发等智能采集模式
- ✅ **批量读取优化** - 减少网络往返，提升采集效率
- ✅ **配置热更新** - JSON 配置 + 文件监控，无需重启
- ✅ **实时监控** - Prometheus 指标 + Vue3 可视化界面
- ✅ **双存储策略** - InfluxDB + Parquet 本地持久化
- ✅ **自动重试机制** - 网络异常自动重连，数据重传

## 🏗️ 系统架构

### 整体架构图

```
┌────────────────────────────┐        ┌────────────────────────────────┐
│          PLC 设备           │──────▶│        心跳监控层                │
│       PLC Device           │        │   Heartbeat Monitor            │
└────────────────────────────┘        └────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│        数据采集层           │
│   Data Collection Layer     │
│       (ChannelCollector)    │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│        数据处理层           │
│     Data Processing Layer   │
│       (DataProcessing)      │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│        队列服务层           │
│      Queue Service Layer    │
│         (LocalQueue)        │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐
│       存储层（双模式）       │
│     Storage Layer (Dual)    │
└────────────────────────────┘
                 │
                 ▼
┌────────────────────────────┐        ┌────────────────────────────────┐
│        WAL 持久化           │──────▶│        时序数据库存储           │
│   WAL Persistence (Parquet) │        │  Time-Series DB Storage        │
│         (Parquet)           │        │          (InfluxDB)            │
└────────────────────────────┘        └────────────────────────────────┘
                 │                                 │
                 ▼                                 │ 写入失败 / Write Failed
┌────────────────────────────┐                     │
│         重试工作器          │◀────────────────────┘
│      Retry Worker          │
│      (RetryWorker)         │
└────────────────────────────┘
```

### 核心数据流

1. **采集阶段**: PLC → ChannelCollector → DataProcessingService
2. **聚合阶段**: LocalQueueService (按 BatchSize 聚合)
3. **持久化阶段**: Parquet WAL (立即写入) → InfluxDB (立即写入)
4. **容错阶段**: 成功删除 WAL 文件，失败由 RetryWorker 重试

## 📁 项目结构

```
DataAcquisition/
├── DataAcquisition.Application/     # 应用层 - 接口定义
│   ├── Abstractions/               # 核心接口抽象
│   └── PlcRuntime.cs              # PLC 运行时枚举
├── DataAcquisition.Domain/         # 领域层 - 核心模型
│   ├── Models/                     # 数据模型
│   └── OperationalEvents/          # 操作事件
├── DataAcquisition.Infrastructure/ # 基础设施层 - 实现
│   ├── Clients/                    # PLC 客户端实现
│   ├── DataAcquisitions/           # 数据采集服务
│   ├── DataStorages/               # 数据存储服务
│   └── Metrics/                    # 指标收集
├── DataAcquisition.Gateway/        # 网关层 - Web API
│   ├── Configs/                    # 设备配置文件
│   ├── Controllers/                # API 控制器
│   ├── Services/                   # 网关服务
│   └── Views/                      # 视图页面
└── DataAcquisition.sln             # 解决方案文件
```

## 🚀 快速开始

### 环境要求

- .NET 10.0 或 .NET 8.0 SDK（推荐使用最新 LTS 版本）
- InfluxDB 2.x (可选，用于时序数据存储)
- 支持的 PLC 设备（Modbus TCP, Beckhoff ADS, Inovance, Mitsubishi, Siemens）

> **注意**: 项目支持多目标框架（.NET 10.0、.NET 8.0），可根据部署环境选择合适的版本。两个版本均为 LTS（长期支持）版本，适合生产环境使用。
>
> **版本选择建议**:
>
> - **.NET 10.0**: 最新 LTS 版本，支持至 2028 年，推荐用于新部署
> - **.NET 8.0**: 稳定 LTS 版本，支持至 2026 年，推荐用于生产环境

### 安装步骤

1. **克隆项目**

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
```

2. **恢复依赖**

```bash
dotnet restore
```

3. **配置设备**
   编辑 `DataAcquisition.Gateway/Configs/M01C123.json` 文件，配置您的 PLC 设备信息。

4. **运行系统**

```bash
# 使用默认框架运行
dotnet run --project DataAcquisition.Gateway

# 或指定特定框架运行
dotnet run -f net10.0 --project DataAcquisition.Gateway
dotnet run -f net8.0 --project DataAcquisition.Gateway
```

5. **构建特定框架**

```bash
# 构建所有目标框架
dotnet build

# 构建特定框架
dotnet build -f net10.0
dotnet build -f net8.0
```

6. **访问监控界面**

- 指标可视化: http://localhost:8000/metrics
- Prometheus 指标: http://localhost:8000/metrics
- API 文档: 未配置 Swagger（可通过代码启用）

## ⚙️ 配置说明

### 设备配置文件示例

```json
{
  "IsEnabled": true,
  "PLCCode": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "temperature",
      "ChannelCode": "PLC01C01",
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Conditional",
      "EnableBatchRead": true,
      "BatchReadRegister": "D200",
      "BatchReadLength": 20,
      "DataPoints": [
        {
          "FieldName": "temp_value",
          "Register": "D200",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value * 0.1"
        }
      ],
      "ConditionalAcquisition": {
        "Register": "D210",
        "DataType": "short",
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

### 应用配置 (appsettings.json)

```json
{
  "InfluxDb": {
    "Url": "http://localhost:8086",
    "Token": "your-token",
    "Org": "your-org",
    "Bucket": "your-bucket"
  },
  "Parquet": {
    "Directory": "./Data/parquet",
    "MaxFileSize": 104857600,
    "MaxFileAge": 86400
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### 📊 配置到数据库映射说明

系统将配置文件映射到 InfluxDB 时序数据库，以下是映射关系：

#### 映射关系表

| 配置文件字段                        | InfluxDB 结构           | 说明                           | 示例值                       |
| ----------------------------------- | ----------------------- | ------------------------------ | ---------------------------- |
| `Channels[].Measurement`            | **Measurement**         | 时序数据库的测量名称（表名）   | `"sensor"`                   |
| `PLCCode`                           | **Tag**: `plc_code`     | PLC 设备编码标签               | `"M01C123"`                  |
| `Channels[].ChannelCode`            | **Tag**: `channel_code` | 通道编码标签                   | `"M01C01"`                   |
| `EventType`                         | **Tag**: `event_type`   | 事件类型标签（Start/End/Data） | `"Start"`, `"End"`, `"Data"` |
| `Channels[].DataPoints[].FieldName` | **Field**               | 数据字段名称                   | `"up_temp"`, `"down_temp"`   |
| `CycleId`                           | **Field**: `cycle_id`   | 采集周期唯一标识符（GUID）     | `"guid-xxx"`                 |
| 采集时间                            | **Timestamp**           | 数据点的时间戳                 | `2025-01-15T10:30:00Z`       |

#### 配置示例与 Line Protocol

**配置文件** (`M01C123.json`):

```json
{
  "PLCCode": "M01C123",
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "M01C01",
      "DataPoints": [
        {
          "FieldName": "up_temp",
          "Register": "D6002",
          "Index": 2,
          "DataType": "short"
        },
        {
          "FieldName": "down_temp",
          "Register": "D6004",
          "Index": 4,
          "DataType": "short",
          "EvalExpression": "value / 1000.0"
        }
      ],
      "ConditionalAcquisition": {
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

**生成的 InfluxDB Line Protocol**:

**Start 事件**（条件采集开始）:

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Start up_temp=250i,down_temp=0.18,cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312200000000000
```

**Data 事件**（普通数据点）:

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Data up_temp=255i,down_temp=0.19 1705312210000000000
```

**End 事件**（条件采集结束）:

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=End cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312300000000000
```

#### Line Protocol 格式说明

InfluxDB Line Protocol 格式：

```
measurement,tag1=value1,tag2=value2 field1=value1,field2=value2 timestamp
```

**字段类型说明**：

- **Measurement**: 来自配置的 `Measurement`，例如 `"sensor"`
- **Tags**（用于过滤和分组，索引字段）:
  - `plc_code`: PLC 设备编码
  - `channel_code`: 通道编码
  - `event_type`: 事件类型（`Start`/`End`/`Data`）
- **Fields**（实际数据值）:
  - 来自 `DataPoints[].FieldName` 的所有字段（如 `up_temp`, `down_temp`）
  - `cycle_id`: 条件采集的周期 ID（GUID，用于关联 Start/End 事件）
  - 数值类型：整数使用 `i` 后缀（如 `250i`），浮点数直接写（如 `0.18`）
- **Timestamp**: 数据采集时间（纳秒精度）

#### 查询示例

**查询特定 PLC 的采集通道的指定时间（1h）范围的数据**:

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["plc_code"] == "M01C123")
  |> filter(fn: (r) => r["channel_code"] == "M01C01")
```

**查询条件采集的完整周期**:

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["cycle_id"] == "550e8400-e29b-41d4-a716-446655440000")
```

## 🔌 API 使用示例

### 指标数据查询

```bash
# 获取 Prometheus 格式指标
curl http://localhost:8000/metrics

# 获取 JSON 格式指标
curl http://localhost:8000/api/metrics-data

# 获取指标信息
curl http://localhost:8000/api/metrics-data/info
```

### PLC 连接状态查询

```bash
# 获取 PLC 连接状态
curl http://localhost:8000/api/DataAcquisition/GetPlcConnectionStatus
```

### PLC 写入操作

```csharp
// C# 客户端示例
var request = new PlcWriteRequest
{
    PlcCode = "M01C123",
    Items = new List<PlcWriteItem>
    {
        new PlcWriteItem
        {
            Address = "D300",
            DataType = "short",
            Value = 100
        }
    }
};

var response = await httpClient.PostAsJsonAsync("/api/DataAcquisition/WriteRegister", request);
```

## 📊 核心模块说明

### PLC 客户端实现

| 协议         | 实现类                        | 描述                  |
| ------------ | ----------------------------- | --------------------- |
| Mitsubishi   | `MitsubishiPlcClientService`  | 三菱 PLC 通讯客户端   |
| Inovance     | `InovancePlcClientService`    | 汇川 PLC 通讯客户端   |
| Beckhoff ADS | `BeckhoffAdsPlcClientService` | 倍福 ADS 协议客户端   |
| Siemens      | `SiemensPlcClientService`     | 西门子 PLC 通讯客户端 |

### ChannelCollector - 通道采集器

```csharp
public class ChannelCollector : IChannelCollector
{
    public async Task CollectAsync(DeviceConfig config, DataAcquisitionChannel channel,
        IPlcClientService client, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            // 检查 PLC 连接状态
            if (!await WaitForConnectionAsync(config, ct))
                continue;

            // 获取设备锁，确保线程安全的 PLC 访问
            if (!_plcLifecycle.TryGetLock(config.PLCCode, out var locker))
                continue;

            await locker.WaitAsync(ct);
            try
            {
                var timestamp = DateTime.Now;

                // 处理不同的采集模式
                if (channel.AcquisitionMode == AcquisitionMode.Always)
                {
                    await HandleUnconditionalCollectionAsync(config, channel, client, timestamp, ct);
                }
                else if (channel.AcquisitionMode == AcquisitionMode.Conditional)
                {
                    await HandleConditionalCollectionAsync(config, channel, client, timestamp, ct);
                }
            }
            finally
            {
                locker.Release();
            }
        }
    }
}
```

### InfluxDbDataStorageService - 数据存储服务

```csharp
public class InfluxDbDataStorageService : IDataStorageService
{
    public async Task SaveAsync(DataMessage dataMessage)
    {
        _writeStopwatch.Restart();
        try
        {
            // 将数据消息转换为 InfluxDB 数据点
            var point = ConvertToPoint(dataMessage);

            // 使用 WriteApi 写入 InfluxDB
            using var writeApi = _client.GetWriteApi();
            writeApi.WritePoint(_bucket, _org, point);

            // 记录监控指标
            _writeStopwatch.Stop();
            _metricsCollector?.RecordWriteLatency(dataMessage.Measurement, _writeStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // 记录错误指标并记录异常
            _metricsCollector?.RecordError(dataMessage.PLCCode ?? "unknown",
                dataMessage.Measurement, dataMessage.ChannelCode);
            _logger.LogError(ex, "[ERROR] InfluxDB 写入失败: {Message}", ex.Message);
        }
    }

    public async Task SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return;

        _writeStopwatch.Restart();
        try
        {
            // 批量转换消息为数据点
            var points = dataMessages.Select(ConvertToPoint).ToList();

            // 批量写入 InfluxDB
            using var writeApi = _client.GetWriteApi();
            writeApi.WritePoints(_bucket, _org, points);

            // 记录批量效率指标
            _writeStopwatch.Stop();
            var batchSize = dataMessages.Count;
            _metricsCollector?.RecordBatchWriteEfficiency(batchSize, _writeStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // 处理批量写入错误
            var plcCode = dataMessages.FirstOrDefault()?.PLCCode ?? "unknown";
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            _metricsCollector?.RecordError(plcCode, measurement, null);
            _logger.LogError(ex, "[ERROR] InfluxDB 批量写入失败: {Message}", ex.Message);
        }
    }
}
```

### MetricsCollector - 指标收集器

系统内置以下核心监控指标：

#### 采集指标

- **`data_acquisition_collection_latency_ms`** - 采集延迟（从 PLC 读取到写入数据库的时间，毫秒）
- **`data_acquisition_collection_rate`** - 采集频率（每秒采集的数据点数，points/s）

#### 队列指标

- **`data_acquisition_queue_depth`** - 队列深度（Channel 待读取 + 批量积累的待处理消息总数）
- **`data_acquisition_processing_latency_ms`** - 处理延迟（队列处理延迟，毫秒）

#### 存储指标

- **`data_acquisition_write_latency_ms`** - 写入延迟（数据库写入延迟，毫秒）
- **`data_acquisition_batch_write_efficiency`** - 批量写入效率（批量大小/写入耗时，points/ms）

#### 错误与连接指标

- **`data_acquisition_errors_total`** - 错误总数（按设备/通道统计）
- **`data_acquisition_connection_status_changes_total`** - 连接状态变化总数
- **`data_acquisition_connection_duration_seconds`** - 连接持续时间（秒）

## 🔄 数据处理流程

### 正常流程

1. **数据采集**: ChannelCollector 从 PLC 读取数据
2. **数据处理**: DataProcessingService 进行数据转换和验证
3. **队列聚合**: LocalQueueService 按 BatchSize 聚合数据
4. **WAL 写入**: 立即写入 Parquet 文件作为预写日志
5. **主存储写入**: 立即写入 InfluxDB
6. **WAL 清理**: 写入成功则删除对应的 Parquet 文件

### 异常处理流程

1. **网络异常**: 自动重连机制，心跳监控确保连接状态
2. **存储失败**: WAL 文件保留，由 ParquetRetryWorker 定期重试
3. **配置错误**: 配置验证和热重载机制

## 🎯 性能优化建议

### 采集参数调优

| 参数                | 推荐值    | 说明              |
| ------------------- | --------- | ----------------- |
| BatchSize           | 10-50     | 平衡延迟和吞吐量  |
| AcquisitionInterval | 100-500ms | 根据 PLC 性能调整 |
| HeartbeatInterval   | 5000ms    | 连接监控频率      |

### 存储优化

- **Parquet 压缩**: 使用 Snappy 压缩减少磁盘占用
- **文件滚动**: 配置 MaxFileSize 和 MaxFileAge 避免文件过大
- **重试间隔**: RetryWorker 默认 5 秒，可根据网络状况调整

## ❓ 常见问题 (FAQ)

### Q: 数据丢失怎么办？

A: 系统采用 WAL-first 架构，所有数据先写入 Parquet 文件，再写入 InfluxDB。只有两者都成功才会删除 WAL 文件，确保数据零丢失。

### Q: 如何添加新的 PLC 协议？

A: 实现 `IPlcClientService` 接口，并在 `PlcClientFactory` 中注册新的协议支持。

### Q: 配置修改后需要重启吗？

A: 不需要。系统使用 FileSystemWatcher 监控配置文件变化，支持热更新。

### Q: 监控指标在哪里查看？

A: 访问 http://localhost:8000/metrics 查看可视化界面，或 http://localhost:8000/metrics/raw 获取 Prometheus 格式指标。

### Q: 如何扩展存储后端？

A: 实现 `IDataStorageService` 接口，保持与队列服务的写入契约一致性。

## 🏆 设计理念

### WAL-first 架构

系统核心设计理念是"数据安全第一"。所有数据采集后立即写入本地 Parquet 文件作为预写日志，然后再异步写入 InfluxDB。这种设计确保即使在网络故障、存储服务不可用等异常情况下，数据也不会丢失。

### 模块化设计

系统采用清晰的分层架构，各模块通过接口抽象，支持灵活扩展和替换。新的 PLC 协议、存储后端、数据处理逻辑都可以通过实现相应接口快速集成。

### 运维友好

内置完整的监控指标和可视化界面，支持配置热更新，提供详细的日志记录，大大降低了运维复杂度。

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
- [Element Plus](https://element-plus.org/) - Vue 3 组件库

---

**如有问题或建议，请提交 [Issue](https://github.com/your-username/G-DataAcquisition/issues) 或通过 Pull Request 贡献代码！**
