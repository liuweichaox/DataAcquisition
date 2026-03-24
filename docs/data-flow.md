# 🔄 数据处理流程

本文档详细说明 DataAcquisition 系统的数据处理流程。

## 概览

本页聚焦数据流转与 WAL 机制。完整导航见索引页。

## 正常流程

1. **数据采集**: ChannelCollector 从 PLC 读取数据
2. **队列聚合**: QueueService 按 BatchSize 聚合数据
3. **WAL 写入**: 立即写入 Parquet 文件作为预写日志
4. **主存储写入**: 立即写入 InfluxDB
5. **WAL 清理**: 写入成功则删除对应的 Parquet 文件

## 异常处理流程

### 网络异常

- **自动重连机制**: 系统自动检测 PLC 连接状态，断开后自动重连
- **心跳监控**: 通过心跳寄存器监控 PLC 连接状态
- **连接状态记录**: 记录连接状态变化，便于问题排查

### 存储失败

- **WAL 文件移动**: InfluxDB 写入失败时，Parquet WAL 文件从 `pending` 文件夹移动到 `retry` 文件夹
- **自动重试**: 由 ParquetRetryWorker 定期扫描 `retry` 文件夹并重试写入
- **文件夹隔离**: 使用两个文件夹（`pending` 和 `retry`）完全隔离，避免并发冲突
- **重试策略**: 支持配置重试间隔和最大重试次数

### 配置错误

- **配置验证**: 启动时验证配置文件格式和完整性
- **热重载机制**: 使用 FileSystemWatcher 监控配置文件变化，支持热更新
- **错误日志**: 配置错误时记录详细日志，便于排查

## 数据流转图

```
PLC Device
    ↓
ChannelCollector (采集)
    ↓
QueueService (队列聚合)
    ↓
    ├─→ ParquetFileStorageService (WAL 写入到 pending 文件夹)
    │       ↓
    │   写入成功 → 删除 WAL 文件
    │   写入失败 → 移动到 retry 文件夹 → RetryWorker 重试
    │
    └─→ InfluxDbDataStorageService (主存储写入)
            ↓
        写入成功 → 完成
        写入失败 → WAL 文件移动到 retry 文件夹 → RetryWorker 重试
```

## 数据一致性与可恢复性

- **WAL-first 架构**: 健康消息会先写入本地 Parquet WAL，再尝试进入主存储
- **批次降级**: 批量 WAL 写失败时，系统会退化为逐条处理，尽量保住健康消息
- **坏消息审计**: 无法写入 WAL 的消息会进入 `invalid/`，不会拖死整批数据
- **重试回放**: `retry/` 中的文件由后台 Worker 重放，尽量恢复主存储一致性

> 关于性能优化建议，请参考 [性能优化文档](performance.md)

## 下一步

理解数据处理流程后，建议继续学习：

- [文档索引](index.md)
- [设计理念](design.md)
- [性能优化建议](performance.md)
