# 📦 核心模块

本文档按“PLC 数据采集主线”介绍当前项目的核心模块和职责划分。

## 模块总览

### 1. PLC 驱动层

位置：

- `src/DataAcquisition.Application/Abstractions/IPlcClientService.cs`
- `src/DataAcquisition.Application/Abstractions/IPlcClientFactory.cs`
- `src/DataAcquisition.Infrastructure/Clients/HslStandardPlcDriverProvider.cs`
- `src/DataAcquisition.Infrastructure/Clients/HslPlcClientService.cs`

职责：

- 用稳定 `Driver` 名称选择 PLC 通讯实现
- 屏蔽上层对 HslCommunication 的直接依赖
- 校验并应用 `ProtocolOptions`

当前默认实现：

- `HslStandardPlcDriverProvider`
- `HslPlcClientService`

### 2. 采集编排层

位置：

- `src/DataAcquisition.Infrastructure/DataAcquisitions/DataAcquisitionService.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/HeartbeatMonitor.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/ChannelCollector.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/AcquisitionStateManager.cs`

职责：

- 按设备和通道启动采集任务
- 管理 PLC 连接健康状态
- 执行 Always / Conditional 采集
- 管理 active cycle，并在重启后恢复状态

这里是整个 PLC 采集主线的核心。

### 3. 队列与持久化层

位置：

- `src/DataAcquisition.Infrastructure/Queues/QueueService.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/ParquetFileStorageService.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/InfluxDbDataStorageService.cs`

职责：

- 批量聚合数据消息
- 先写 WAL，再写主存储
- 对主存储失败进行重试
- 对无法写入 WAL 的坏消息做 `invalid/` 隔离

关键目录：

- `pending/`
- `retry/`
- `invalid/`

### 4. 配置与运维层

位置：

- `src/DataAcquisition.Infrastructure/DeviceConfigs/DeviceConfigService.cs`
- `src/DataAcquisition.Infrastructure/Metrics/*`
- `src/DataAcquisition.Infrastructure/Logs/*`

职责：

- 设备配置加载与热更新
- Prometheus 指标
- SQLite 日志查询

### 5. 宿主层

位置：

- `src/DataAcquisition.Edge.Agent/Program.cs`
- `src/DataAcquisition.Edge.Agent/BackgroundServices/*`
- `src/DataAcquisition.Central.Api/*`
- `src/DataAcquisition.Central.Web/*`

职责：

- Edge Agent：采集宿主、后台 Worker、本地诊断 API
- Central API：节点注册、心跳、诊断代理
- Central Web：集中查看状态和指标

## 关键运行链路

### Always / Conditional 数据采集

主流程：

1. `DataAcquisitionService` 启动每个设备/通道的采集循环
2. `HeartbeatMonitor` 判断 PLC 是否可采
3. `ChannelCollector` 从 PLC 读取数据
4. 生成 `DataMessage`
5. 交给 `QueueService`
6. `QueueService` 先写 WAL，再写主存储

### 条件采集恢复

条件采集额外依赖：

- `AcquisitionStateManager`

当前行为：

- active cycle 同时保存在内存和 SQLite
- 首拍只建立基线，不伪造正常 `Start/End`
- 必要时写入 `RecoveredStart` / `Interrupted`
- 恢复诊断写入 `<measurement>_diagnostic`
- 采集消息统一使用 UTC 时间戳

正式周期统计时，应只把成对的 `Start` / `End` 视为完整周期。

## 当前最重要的扩展点

### 扩展新的 PLC 驱动

1. 实现新的 `IPlcDriverProvider`
2. 如果需要，增加新的 `IPlcClientService` 实现
3. 在 `Program.cs` 中注册 provider
4. 在配置中使用完整 `Driver` 名称

### 替换主存储

1. 实现 `IDataStorageService`
2. 在 Edge Agent 中替换默认注册

### 替换 WAL

1. 实现 `IWalStorageService`
2. 确保保留 `pending/retry/invalid` 这类状态语义

## 自动化测试

测试项目位置：

- `tests/DataAcquisition.Core.Tests`

当前优先覆盖的行为：

- 驱动配置校验
- active cycle 持久化恢复
- WAL 坏消息隔离

## 相关阅读

- [架构设计](design.md)
- [数据流](data-flow.md)
- [配置教程](tutorial-configuration.md)
