# 🛰️ PLC 数据采集系统 (中文)

.NET 8 | Windows / Linux / macOS  
English version: [README.en.md](README.en.md)

## 📙 概述
- 多 PLC 并行采集，条件/无条件采集。
- BatchSize 全链路一致：凑满后立即写 Parquet（WAL），立刻写 Influx，成功即删 WAL；失败保留，RetryWorker 每 5 秒兜底重传。
- 配置热更新（JSON + FileSystemWatcher）。
- 指标：Prometheus `/metrics/raw`，Vue3 + Element Plus 指标页 `/metrics`（多选筛选、记忆选择）。
- 时间全部使用本地时间（不转换 UTC）。

## 🏗️ 架构与核心组件
```
PLC → HeartbeatMonitor → ChannelCollector → DataProcessingService
   → LocalQueueService（批量聚合）
   → Parquet WAL（凑满 BatchSize）
   → 立即写 Influx（成功删 WAL，失败保留）
   → ParquetRetryWorker（5s 扫描重传）
```
- 采集：`ChannelCollector`、`HeartbeatMonitor`、`DataAcquisitionService`
- 队列：`LocalQueueService`（内存凑批 → 写 WAL → 立刻写 Influx）
- 存储：`ParquetFileStorageService`（Snappy），`InfluxDbDataStorageService`
- 后台：`ParquetRetryWorker`（5 秒兜底重传）
- 配置：`DeviceConfigService`（JSON 热更新）
- 指标：`MetricsCollector`、`/metrics/raw`、`/metrics`

## 🚀 快速开始
```bash
dotnet restore
dotnet build
dotnet run --project DataAcquisition.Gateway
# 访问 http://localhost:8000/metrics （可视化） /metrics/raw（Prometheus）
```

## ⚙️ 配置要点（位于 `DataAcquisition.Gateway/Configs/*.json`）
- 设备：`IsEnabled`, `Code`, `Host`, `Port`, `Type`(ModbusTcp/…)；心跳：`HeartbeatMonitorRegister`, `HeartbeatPollingInterval`(ms)
- 通道：`Measurement`, `BatchSize`（全链路批大小）, `AcquisitionInterval`(ms，0=尽快), `EnableBatchRead`, `BatchReadRegister`, `BatchReadLength`
- 数据点：`FieldName`, `Register`, `Index`(批读偏移), `DataType`(short/int/float/double/bool/string), `EvalExpression`(使用 `value`)
- 条件采集：`ConditionalAcquisition.Register/DataType`, `Start/End.TriggerMode`(Always/RisingEdge/FallingEdge/ValueIncrease/ValueDecrease), 可选 `TimestampField`

示例：
```json
{
  "IsEnabled": true,
  "Code": "M01C123",
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
      "DataPoints": [
        { "FieldName": "temp_value", "Register": "D200", "DataType": "float", "EvalExpression": "value * 0.1" }
      ]
    }
  ]
}
```

## 🔧 API / 前端
- Prometheus：`/metrics/raw`
- 指标页：`/metrics`（多选筛选、记忆选择）
- SignalR Hub：`/dataHub`（实时推送，视代码示例）
- 示例 API：`GET /api/metrics-data`（JSON 指标）

## 📌 配置优化建议
- BatchSize：小批量(1-10)低延迟；中批量(10-50)通用；大批量(50+)吞吐优先。
- 采集间隔：1-100ms 高频，100-1000ms 常规，>1000ms 慢变。
- 批量读：连续寄存器启用 `EnableBatchRead`，设置起始/长度与 Index。

## 🛠️ 扩展
- PLC 通讯：实现 `IPlcClientService` / `IPlcClientFactory`
- 数据处理：实现 `IDataProcessingService`
- 配置：实现 `IDeviceConfigService`
- 存储：可替换 `IDataStorageService`（保持与队列写入契约）

## 🚢 部署
```bash
dotnet publish DataAcquisition.Gateway -c Release -r win-x64 --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r linux-x64 --self-contained true
```

## 📜 许可
MIT，详见 LICENSE。

