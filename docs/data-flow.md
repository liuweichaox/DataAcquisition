# 🔄 数据处理流程

本文档详细说明 DataAcquisition 系统的数据处理流程。

## 相关文档

- [设计理念](design.md) - 了解系统设计思想
- [核心模块文档](modules.md) - 了解系统核心模块

## 正常流程

1. **数据采集**: ChannelCollector 从 PLC 读取数据
2. **队列聚合**: LocalQueueService 按 BatchSize 聚合数据
3. **WAL 写入**: 立即写入 Parquet 文件作为预写日志
4. **主存储写入**: 立即写入 InfluxDB
5. **WAL 清理**: 写入成功则删除对应的 Parquet 文件

## 异常处理流程

### 网络异常

- **自动重连机制**: 系统自动检测 PLC 连接状态，断开后自动重连
- **心跳监控**: 通过心跳寄存器监控 PLC 连接状态
- **连接状态记录**: 记录连接状态变化，便于问题排查

### 存储失败

- **WAL 文件保留**: InfluxDB 写入失败时，Parquet WAL 文件保留
- **自动重试**: 由 ParquetRetryWorker 定期重试写入
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
LocalQueueService (队列聚合)
    ↓
    ├─→ ParquetFileStorageService (WAL 写入)
    │       ↓
    │   写入成功 → 删除 WAL 文件
    │   写入失败 → 保留 WAL 文件 → RetryWorker 重试
    │
    └─→ InfluxDbDataStorageService (主存储写入)
            ↓
        写入成功 → 完成
        写入失败 → WAL 文件保留 → RetryWorker 重试
```

## 数据一致性保证

- **WAL-first 架构**: 所有数据先写入本地 Parquet 文件，确保数据不丢失
- **原子性操作**: 批量写入时，要么全部成功，要么全部失败
- **幂等性**: 重试机制保证重复写入不会导致数据重复

> 关于性能优化建议，请参考 [性能优化文档](performance.md)

## 下一步

理解数据处理流程后，你可以：

- 阅读 [性能优化建议](performance.md) 了解如何优化系统性能
- 阅读 [设计理念](design.md) 了解系统设计思想
- 阅读 [核心模块文档](modules.md) 深入了解系统核心模块
