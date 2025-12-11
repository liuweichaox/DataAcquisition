# 🛰️ PLC 数据采集系统 / PLC Data Acquisition System

.NET 8 | Windows / Linux / macOS
English: [README.en.md](README.en.md)

---

## 📙 概述

- 多 PLC 并行采集，支持条件/无条件与批量读取。
- BatchSize 全链路一致：凑满 BatchSize 立刻写 Parquet（WAL），立即写 Influx，成功即删 WAL；失败保留，RetryWorker 每 5 秒兜底重传。
- 配置热更新（JSON + FileSystemWatcher）；全部使用本地时间；Prometheus 指标 + Vue3/Element Plus 指标页（多选筛选、选择记忆）。

## 🏗️ 架构与核心流程

```
PLC → HeartbeatMonitor → ChannelCollector → DataProcessingService
   → LocalQueueService (BatchSize 聚合)
   → Parquet WAL (Snappy)
   → 立即写 InfluxDB（成功删文件，失败保留）
   → ParquetRetryWorker (5s 扫描重试)
```

组件：采集（ChannelCollector/HeartbeatMonitor/DataAcquisitionService）、队列（LocalQueueService）、存储（Parquet/Influx）、后台（ParquetRetryWorker）、配置（DeviceConfigService）、指标（MetricsCollector + /metrics/raw + /metrics）。

## 🚀 快速开始

```bash
dotnet restore
dotnet build
dotnet run --project DataAcquisition.Gateway
# 浏览: http://localhost:8000/metrics (UI)   http://localhost:8000/metrics/raw (Prometheus)
```

## ⚙️ 采集配置（YAML 说明；实际为 JSON 文件 `DataAcquisition.Gateway/Configs/*.json`）

```yaml
IsEnabled: true # 是否启用设备
Code: "PLC01" # 设备编码
Host: "192.168.1.100" # PLC IP
Port: 502 # 端口
Type: ModbusTcp # PLC 类型 (ModbusTcp/...)
HeartbeatMonitorRegister: "D100" # 心跳寄存器
HeartbeatPollingInterval: 5000 # 心跳间隔(ms)

Channels:
  - Measurement: "temperature" # 时序库测量名
    BatchSize: 10 # 批大小（全链路一致）
    AcquisitionInterval: 100 # 采集间隔(ms)，0=尽快
    AcquisitionMode: Conditional # Conditional 条件采集 | Always 无条件采集
    EnableBatchRead: true
    BatchReadRegister: "D200"
    BatchReadLength: 20
    DataPoints:
      - FieldName: "temp_value" # 字段名
        Register: "D200" # 寄存器
        Index: 0 # 批读偏移
        DataType: float # 类型
        EvalExpression: "value * 0.1" # 表达式
    ConditionalAcquisition:
      Register: "D210"
      DataType: short
      Start:
        TriggerMode: RisingEdge # Always/RisingEdge/FallingEdge/ValueIncrease/ValueDecrease
        TimestampField: "start_time"
      End:
        TriggerMode: FallingEdge
        TimestampField: "end_time"
```

### 关键字段说明

- `BatchSize`：采集 → 队列 →WAL→Influx 同一批大小；凑满即写 WAL + Influx，成功删文件。
- `AcquisitionInterval`：0 表示尽可能快；高频需关注 PLC 负载。
- 批量读：连续寄存器启用 `EnableBatchRead`，设置起始/长度与 `Index` 对应。
- 条件采集：Start/End 多种触发模式；`TimestampField` 可写起止时间。
- WAL 目录/滚动：`Parquet:Directory`、`Parquet:MaxFileSize`、`Parquet:MaxFileAge`。

## 🔧 API / 前端

- Prometheus 原始指标：`/metrics/raw`
- 指标可视化：`/metrics`（多选筛选，选择记忆）
- SignalR Hub：`/dataHub`（实时推送，参考代码）
- 示例：`GET /api/metrics-data`（指标 JSON）

## 📊 指标（示例）

- 采集延迟/频率：`data_acquisition_collection_latency_ms` / `data_acquisition_collection_rate`
- 队列深度：`data_acquisition_queue_depth`
- 写入延迟：`data_acquisition_write_latency_ms`
- 错误计数：`data_acquisition_errors_total`
- 连接状态：`data_acquisition_connection_status_changes_total`，`data_acquisition_connection_duration_seconds`
- HTTP：`http_request_duration_seconds` 等 Prometheus 默认指标

## 📌 调优建议

- BatchSize：1-10 低延迟；10-50 通用；50+ 吞吐优先。
- 采集间隔：1-100ms 高频；100-1000ms 常规；>1000ms 慢变。
- Influx/WAL：若希望更低延迟，可调小 BatchSize 或 Flush/Retry 间隔；目录/滚动阈值按磁盘与吞吐调整。

## 🛠️ 扩展

- 通讯：实现 `IPlcClientService` / `IPlcClientFactory`
- 预处理：实现 `IDataProcessingService`
- 配置：实现 `IDeviceConfigService`
- 存储：可替换 `IDataStorageService`（保持队列写入契约）

## 🚢 发布

```bash
dotnet publish DataAcquisition.Gateway -c Release -r win-x64 --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r linux-x64 --self-contained true
```

## 📜 许可

MIT，详见 LICENSE。
