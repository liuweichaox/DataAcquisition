# 📊 核心模块文档

本文档介绍 DataAcquisition 系统的核心模块设计和使用方法。

## 相关文档

- [快速开始指南](getting-started.md) - 从零开始使用系统
- [配置说明](configuration.md) - 详细的配置选项说明
- [API 使用示例](api-usage.md) - API 接口使用方法
- [性能优化建议](performance.md) - 优化系统性能

## PLC 客户端实现

系统支持多种 PLC 协议，每个协议都有对应的客户端实现：

| 协议         | 实现类                        | 描述                  |
| ------------ | ----------------------------- | --------------------- |
| Mitsubishi   | `MitsubishiPlcClientService`  | 三菱 PLC 通信客户端   |
| Inovance     | `InovancePlcClientService`    | 汇川 PLC 通信客户端   |
| BeckhoffAds  | `BeckhoffAdsPlcClientService` | 倍福 ADS 协议客户端   |

## ChannelCollector - 通道采集器

`ChannelCollector` 是系统的核心采集组件，负责从 PLC 读取数据。

### 功能特性

- **自动连接管理**: 自动检测和处理 PLC 连接状态，断开后自动重连
- **多种采集模式**: 支持持续采集（Always）和条件触发采集（Conditional）
- **批量读取优化**: 支持批量读取多个连续寄存器，减少网络往返，提高采集效率
- **线程安全**: 确保同一 PLC 设备的并发访问安全

### 采集模式说明

#### Always 模式（持续采集）

按配置的 `AcquisitionInterval` 间隔持续采集数据。

**适用场景**：
- 需要持续监控的传感器数据（温度、压力等）
- 需要固定频率采集的数据

**配置示例**：
```json
{
  "AcquisitionMode": "Always",
  "AcquisitionInterval": 100,
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
```

#### Conditional 模式（条件触发采集）

根据指定寄存器的值变化触发采集。

**适用场景**：
- 生产周期管理（生产开始/结束事件）
- 设备状态变化记录
- 按条件触发的数据采集

**配置示例**：
```json
{
  "AcquisitionMode": "Conditional",
  "Metrics": null,
  "ConditionalAcquisition": {
    "Register": "D6006",
    "DataType": "short",
    "StartTriggerMode": "RisingEdge",
    "EndTriggerMode": "FallingEdge"
  }
}
```

## InfluxDbDataStorageService - 数据存储服务

`InfluxDbDataStorageService` 负责将采集的数据写入 InfluxDB 时序数据库。

### 功能特性

- **批量写入**: 按配置的 `BatchSize` 批量写入数据，提高写入效率
- **自动重试**: 写入失败时自动保留 WAL 文件，由后台重试机制自动重试
- **性能监控**: 自动记录写入延迟和批量效率指标，便于性能分析
- **数据安全**: 采用 WAL-first 架构，确保数据零丢失

### 配置说明

在 `appsettings.json` 中配置 InfluxDB 连接信息：

```json
{
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token-here",
    "Bucket": "plc_data",
    "Org": "your-org"
  }
}
```

**配置项说明**：
- `Url`: InfluxDB 服务器地址
- `Token`: InfluxDB 认证令牌（建议使用环境变量管理）
- `Bucket`: 数据存储桶名称
- `Org`: InfluxDB 组织名称

## MetricsCollector - 指标收集器

系统内置完整的监控指标，通过 Prometheus 格式暴露。

### 采集指标

- **`data_acquisition_collection_latency_ms`** - 采集延迟（从 PLC 读取到写入数据库的时间，毫秒）
- **`data_acquisition_collection_rate`** - 采集频率（每秒采集的数据点数，points/s）

### 队列指标

- **`data_acquisition_queue_depth`** - 队列深度（Channel 待读取 + 批量积累的待处理消息总数）
- **`data_acquisition_processing_latency_ms`** - 处理延迟（队列处理延迟，毫秒）

### 存储指标

- **`data_acquisition_write_latency_ms`** - 写入延迟（数据库写入延迟，毫秒）
- **`data_acquisition_batch_write_efficiency`** - 批量写入效率（批量大小/写入耗时，points/ms）

### 错误与连接指标

- **`data_acquisition_errors_total`** - 错误总数（按设备/通道统计）
- **`data_acquisition_connection_status_changes_total`** - 连接状态变化总数
- **`data_acquisition_connection_duration_seconds`** - 连接持续时间（秒）

## 系统扩展

系统采用接口抽象设计，支持灵活的扩展：

### 添加新的 PLC 协议

1. 实现 `IPlcClientService` 接口
2. 在 `PlcClientFactory` 中注册新的协议类型
3. 在设备配置中使用新的协议类型

### 添加新的存储后端

1. 实现 `IDataStorageService` 接口
2. 在 `Program.cs` 中注册新的存储服务
3. 系统会同时使用多个存储后端

详细的扩展方法请参考 [FAQ](faq.md) 中的相关说明。

## 下一步

了解核心模块后，建议继续学习：

- 阅读 [数据处理流程](data-flow.md) 理解数据流转过程
