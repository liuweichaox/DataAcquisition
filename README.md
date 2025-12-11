# 🛰️ G-DataAcquisition - 工业级PLC数据采集系统

[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](https://dotnet.microsoft.com/)

English: [README.en.md](README.en.md)

## 📖 项目简介

G-DataAcquisition 是一个基于 .NET 8 构建的高性能、高可靠性的工业数据采集系统，专门为 PLC（可编程逻辑控制器）数据采集场景设计。系统采用 WAL-first 架构，确保数据零丢失，支持多 PLC 并行采集、条件触发采集、批量读取等高级功能。

### 🎯 核心特性

- ✅ **WAL-first 架构** - 写前日志保证数据不丢失
- ✅ **多 PLC 并行采集** - 支持多种 PLC 协议（Modbus, Beckhoff ADS, Inovance, Mitsubishi）
- ✅ **条件触发采集** - 支持边沿触发、值变化触发等智能采集模式
- ✅ **批量读取优化** - 减少网络往返，提升采集效率
- ✅ **配置热更新** - JSON 配置 + 文件监控，无需重启
- ✅ **实时监控** - Prometheus 指标 + Vue3 可视化界面
- ✅ **双存储策略** - InfluxDB + Parquet 本地持久化
- ✅ **自动重试机制** - 网络异常自动重连，数据重传

## 🏗️ 系统架构

### 整体架构图

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   PLC 设备      │───▶│   数据采集层      │───▶│    数据处理层    │
│ (多协议支持)     │    │ (ChannelCollector)│    │ (DataProcessing)│
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                        │                        │
         │                        ▼                        ▼
         │             ┌──────────────────┐    ┌─────────────────┐
         └─────────────│   心跳监控层      │    │    队列服务层    │
                       │ (HeartbeatMonitor)│    │  (LocalQueue)   │
                       └──────────────────┘    └─────────────────┘
                                                                │
                                                                ▼
                       ┌─────────────────────────────────────────────────┐
                       │             存储层 (双模式)                      │
                       │  ┌──────────────┐        ┌─────────────────┐    │
                       │  │  WAL 持久化   │──────▶│  InfluxDB 存储  │    │
                       │  │  (Parquet)   │        │                 │    │
                       │  └──────────────┘        └─────────────────┘    │
                       │           │                        │            │
                       │           ▼                        │            │
                       │  ┌─────────────────┐               │            │
                       │  │  重试工作器     │◀──────────────┘            │
                       │  │ (RetryWorker)   │                            │
                       │  └─────────────────┘                            │
                       └─────────────────────────────────────────────────┘
```

### 核心数据流

1. **采集阶段**: PLC → ChannelCollector → DataProcessingService
2. **聚合阶段**: LocalQueueService (按 BatchSize 聚合)
3. **持久化阶段**: Parquet WAL (立即写入) → InfluxDB (立即写入)
4. **容错阶段**: 成功删除 WAL 文件，失败由 RetryWorker 重试

## 📁 项目结构

```
G-DataAcquisition/
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

- .NET 8.0 SDK
- InfluxDB 2.x (可选，用于时序数据存储)
- 支持的 PLC 设备（Modbus TCP, Beckhoff ADS, Inovance, Mitsubishi）

### 安装步骤

1. **克隆项目**
```bash
git clone https://github.com/your-username/G-DataAcquisition.git
cd G-DataAcquisition
```

2. **恢复依赖**
```bash
dotnet restore
```

3. **配置设备**
编辑 `DataAcquisition.Gateway/Configs/M01C123.json` 文件，配置您的 PLC 设备信息。

4. **运行系统**
```bash
dotnet run --project DataAcquisition.Gateway
```

5. **访问监控界面**
- 指标可视化: http://localhost:8000/metrics
- Prometheus 指标: http://localhost:8000/metrics/raw
- API 文档: http://localhost:8000/swagger (如启用)

## ⚙️ 配置说明

### 设备配置文件示例

```json
{
  "IsEnabled": true,
  "Code": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "ModbusTcp",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "temperature",
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
          "DataType": "float",
          "EvalExpression": "value * 0.1"
        }
      ],
      "ConditionalAcquisition": {
        "Register": "D210",
        "DataType": "short",
        "Start": {
          "TriggerMode": "RisingEdge",
          "TimestampField": "start_time"
        },
        "End": {
          "TriggerMode": "FallingEdge",
          "TimestampField": "end_time"
        }
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

## 🔌 API 使用示例

### 实时数据订阅 (SignalR)

```javascript
// 前端 JavaScript 示例
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/dataHub")
    .build();

connection.on("DataReceived", (data) => {
    console.log("收到数据:", data);
});

connection.start().then(() => {
    console.log("连接成功");
});
```

### 指标数据查询

```bash
# 获取 Prometheus 格式指标
curl http://localhost:8000/metrics/raw

# 获取 JSON 格式指标
curl http://localhost:8000/api/metrics-data
```

### PLC 写入操作

```csharp
// C# 客户端示例
var request = new PlcWriteRequest 
{ 
    DeviceCode = "PLC01", 
    Register = "D300", 
    Value = 100 
};

var response = await httpClient.PostAsJsonAsync("/api/plc/write", request);
```

## 📊 核心模块说明

### ChannelCollector - 通道采集器

```csharp
public class ChannelCollector : IChannelCollector
{
    public async Task StartCollectionAsync(CancellationToken cancellationToken)
    {
        // PLC 连接健康检查
        await CheckPlcConnectionAsync();
        
        // 触发条件评估
        var shouldCollect = await EvaluateTriggerConditionsAsync();
        
        if (shouldCollect)
        {
            // 执行数据采集
            var data = await CollectDataAsync();
            await ProcessAndStoreDataAsync(data);
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
        // 转换为 InfluxDB 数据点
        var point = ConvertToDataPoint(dataMessage);
        
        // 写入 InfluxDB
        try
        {
            await _writeApi.WritePointAsync(point);
            _metricsCollector.RecordWriteLatency(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _metricsCollector.RecordError("influx_write");
            throw;
        }
    }
}
```

### MetricsCollector - 指标收集器

系统内置 9 种核心监控指标：
- `data_acquisition_collection_latency_ms` - 采集延迟
- `data_acquisition_collection_rate` - 采集频率
- `data_acquisition_queue_depth` - 队列深度
- `data_acquisition_write_latency_ms` - 写入延迟
- `data_acquisition_errors_total` - 错误计数
- `data_acquisition_connection_status_changes_total` - 连接状态变化
- `data_acquisition_connection_duration_seconds` - 连接持续时间
- `data_acquisition_batch_size` - 批次大小统计
- `data_acquisition_throughput` - 系统吞吐量

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

| 参数 | 推荐值 | 说明 |
|------|--------|------|
| BatchSize | 10-50 | 平衡延迟和吞吐量 |
| AcquisitionInterval | 100-500ms | 根据 PLC 性能调整 |
| HeartbeatInterval | 5000ms | 连接监控频率 |

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
git clone https://github.com/your-username/G-DataAcquisition.git

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