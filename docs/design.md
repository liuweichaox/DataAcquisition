# 设计说明

本文档说明 DataAcquisition 的核心定位、边界和架构取舍。

## 项目定位

DataAcquisition 的核心不是“中心平台”，而是一个面向工业现场的 PLC 数据采集运行时。

项目优先解决：

- 稳定连接 PLC
- 正确采集寄存器数据
- 先在本地保留可恢复副本
- 再把数据写入主存储
- 在重启、断网或主存储失败时保持行为可恢复、可审计

因此，Edge Agent 是主产品；Central API / Central Web 是辅助控制面和诊断面。

## 核心原则

## 1. Acquisition First

Edge Agent 必须在没有 Central API 的情况下独立运行，至少完成：

- PLC 连接管理
- 通道采集
- WAL 持久化
- 主存储写入
- 失败重试

Central 侧负责：

- 节点注册与心跳
- Edge 指标与日志代理
- 集中诊断与可视化

Central 不属于主采集链路。

## 2. WAL Before Primary Storage

核心数据路径：

`PLC -> ChannelCollector -> QueueService -> WAL -> TSDB`

WAL 是安全边界，不是附属能力。

WAL 生命周期分为：

- `pending/`：刚写入，尚未完成主存储判定
- `retry/`：主存储失败，等待后台回放
- `invalid/`：无法写入 WAL 的坏消息审计目录

设计目标不是“承诺绝对不丢任何数据”，而是在真实故障场景下尽量做到：

- 正常消息先落本地
- 主存储失败可重试
- 坏消息不拖死整批
- 故障行为可解释

## 3. Explicit Runtime Contracts

框架通过接口将运行时契约与默认实现分离：

- `IPlcDriverProvider`
- `IPlcClientService`
- `IPlcConnectionClient`
- `IPlcDataAccessClient`
- `IPlcTypedWriteClient`
- `IDataStorageService`
- `IWalStorageService`
- `IQueueService`
- `IChannelCollector`
- `IAcquisitionStateManager`

这意味着：

- HslCommunication 是默认 PLC 实现，但不是架构前提
- InfluxDB 是默认主存储，但不是唯一主存储
- Parquet 是默认 WAL 实现，但框架本身不绑定具体文件格式

## 4. Restart Recovery Is a Feature

工业采集系统不能假设进程永不重启。

当前恢复设计：

- active cycle 以内存 + SQLite 方式保存
- 条件采集首拍只建立基线，不伪造 `Start/End`
- 重启后如有需要会写入恢复诊断事件
- 恢复诊断进入 `<measurement>_diagnostic`，不污染正式周期 measurement

## 5. Honest Configuration Contracts

配置设计追求“小而稳定”，不追求一个虚假的“大一统模型”。

公共字段：

- `Driver`
- `Host`
- `Port`
- `ProtocolOptions`
- `Channels`

原则：

- 驱动只接受稳定完整名称
- `Host` / `Port` 是公共契约，驱动不能静默忽略
- `ProtocolOptions` 只开放当前驱动真实支持的参数
- 未声明的 `ProtocolOptions` 在运行时直接拒绝

## 运行时结构

### Edge 主链路

1. `DeviceConfigService`
   - 加载配置并处理热更新
2. `PlcClientLifecycleService`
   - 按 `Driver` 创建和管理 PLC 客户端
3. `HeartbeatMonitor`
   - 跟踪连接健康状态
4. `ChannelCollector`
   - 执行 `Always` / `Conditional` 采集
5. `QueueService`
   - 聚合批次并驱动持久化
6. `QueueBatchPersister`
   - 执行 WAL-first 持久化链
7. `ParquetRetryWorker`
   - 回放 `retry/` 中的 WAL
8. `InfluxDbDataStorageService`
   - 默认主存储

### Central 侧

Central API 属于控制面和诊断面：

- `EdgeRegistry`：注册和心跳状态
- `EdgeDiagnosticsController`：日志和指标代理
- `MetricsController`：中心指标观察

## PLC 驱动设计

驱动层是最重要的开源扩展点之一。

当前设计：

- 配置使用稳定 `Driver` 名称
- 默认目录由 `HslStandardPlcDriverProvider` 提供
- 框架核心不直接依赖 Hsl 类型
- 第三方可以通过实现新的 `IPlcDriverProvider` 接入其他协议栈
- 驱动实现可复用 `PlcClientServiceBase`，不需要从零实现一个过大的接口

这使得：

- 普通用户可以直接通过配置接入常见 PLC
- 高级用户可以替换默认驱动实现

## 有意保留的简单性

当前项目没有刻意做成“过度工程”：

- Hsl 驱动目录仍然保持单文件注册表
- `ProtocolOptions` 不是重量级元数据系统
- 恢复诊断只拆到 sibling measurement，而不是再造一个复杂事件总线

这些都是刻意取舍，目的是保持：

- 好上手
- 好读
- 好扩展
- 好维护

## 当前演进方向

如果继续向更成熟的开源项目推进，优先级建议是：

1. 增加主链路自动化测试
2. 补主流驱动的 `ProtocolOptions`
3. 继续收紧查询和文档里对正式周期与恢复诊断的边界
4. 增加更多示例配置与故障排查文档

## 相关阅读

- [数据流](data-flow.md)
- [核心模块](modules.md)
- [驱动清单](hsl-drivers.md)
