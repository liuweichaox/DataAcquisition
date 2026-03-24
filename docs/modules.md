# 模块

这份文档不列所有文件，而是解释这个项目的主要运行面和模块边界。

DataAcquisition 的主产品是 `Edge Agent`。  
其他模块都应该围绕它的采集链路来理解。

## 模块视图

### Domain

位置：

- `src/DataAcquisition.Domain`

职责：

- 定义配置模型
- 定义采集消息
- 定义受控值类型和归一化规则
- 定义不会依赖具体库的基础模型

这里不应该知道 Hsl、InfluxDB、SQLite 或 ASP.NET。

### Application

位置：

- `src/DataAcquisition.Application`

职责：

- 定义运行时抽象
- 定义 PLC 驱动接口
- 定义存储接口
- 定义配置、队列、采集相关契约

这一层回答的是：

- 上层需要什么能力
- 而不是这些能力如何实现

### Infrastructure

位置：

- `src/DataAcquisition.Infrastructure`

职责：

- 提供默认实现
- 封装 Hsl 驱动
- 封装 InfluxDB
- 封装 Parquet WAL
- 封装 SQLite 日志和状态存储
- 实现配置热更新、指标和诊断

这是项目里最大的实现层，但不应该反向定义上层抽象。

### Edge Agent

位置：

- `src/DataAcquisition.Edge.Agent`

职责：

- 作为采集宿主进程启动整个系统
- 注册默认实现
- 运行后台 Worker
- 暴露本地健康、指标、日志和诊断接口

如果你只关心“项目真正做什么”，先看这一层。

### Central API / Central Web

位置：

- `src/DataAcquisition.Central.Api`
- `src/DataAcquisition.Central.Web`

职责：

- 提供中心化状态查看
- 展示心跳、指标和诊断代理

它们是控制面，不是采集主链路。

### Tests

位置：

- `tests/DataAcquisition.Core.Tests`

职责：

- 验证驱动配置契约
- 验证 WAL 行为
- 验证恢复逻辑
- 验证配置校验

## 主运行链路

DataAcquisition 的核心链路很固定：

1. `Edge Agent` 启动
2. 加载设备配置
3. 为每个设备创建 PLC 驱动
4. 启动心跳与采集任务
5. 生成 `DataMessage`
6. 进入 `QueueService`
7. 先写 `Parquet WAL`
8. 再写 `InfluxDB`
9. 主存储失败时进入 `retry/`

这条链路是判断模块边界是否合理的基准。

## 关键模块

### PLC 驱动层

关键文件：

- `src/DataAcquisition.Application/Abstractions/IPlcDriverProvider.cs`
- `src/DataAcquisition.Application/Abstractions/IPlcClientService.cs`
- `src/DataAcquisition.Infrastructure/Clients/PlcClientFactory.cs`
- `src/DataAcquisition.Infrastructure/Clients/HslStandardPlcDriverProvider.cs`
- `src/DataAcquisition.Infrastructure/Clients/HslPlcClientService.cs`

职责：

- 用稳定的 `Driver` 名称选择具体 PLC 实现
- 屏蔽上层对 Hsl 的直接依赖
- 解析并应用驱动相关配置

这个设计的重点是：

- 核心框架不绑死在具体 PLC 类型枚举上
- Hsl 是默认实现，不是架构前提

### 采集编排层

关键文件：

- `src/DataAcquisition.Infrastructure/DataAcquisitions/DataAcquisitionService.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/HeartbeatMonitor.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/ChannelCollector.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/ChannelMetricReader.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/MetricExpressionEvaluator.cs`
- `src/DataAcquisition.Infrastructure/DataAcquisitions/AcquisitionStateManager.cs`

职责：

- 启动和管理采集任务
- 判断设备是否可采
- 执行 Always / Conditional 采集
- 读取字段、计算表达式、生成事件
- 恢复条件采集 active cycle 状态

### 队列与存储层

关键文件：

- `src/DataAcquisition.Infrastructure/Queues/QueueService.cs`
- `src/DataAcquisition.Infrastructure/Queues/QueueBatchPersister.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/ParquetFileStorageService.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/ParquetDataMessageSerializer.cs`
- `src/DataAcquisition.Infrastructure/DataStorages/InfluxDbDataStorageService.cs`

职责：

- 批量聚合消息
- 先写 WAL，再写主存储
- 重放失败数据
- 隔离坏消息

这里是数据安全边界最重要的一层。

### 配置与运维层

关键文件：

- `src/DataAcquisition.Infrastructure/DeviceConfigs/DeviceConfigService.cs`
- `src/DataAcquisition.Infrastructure/DeviceConfigs/DeviceConfigValidator.cs`
- `src/DataAcquisition.Infrastructure/DeviceConfigs/DeviceConfigFileLoader.cs`
- `src/DataAcquisition.Infrastructure/Logs/*`
- `src/DataAcquisition.Infrastructure/Metrics/*`

职责：

- 读取和验证 JSON 配置
- 监控配置目录并热更新
- 提供 Prometheus 指标
- 提供本地日志查询

## 模块边界原则

如果把这个项目做成长期维护的开源项目，我会坚持这些边界：

- `Domain` 不依赖基础设施库
- `Application` 只定义契约
- `Infrastructure` 只实现，不定义上层业务规则
- `Edge Agent` 是主产品，不把中心服务当主链路
- 文档只承诺真实支持的能力

## 扩展点

### 新增 PLC 驱动

做法：

1. 实现新的 `IPlcDriverProvider`
2. 如有需要，提供新的 `IPlcClientService`
3. 在宿主层注册
4. 提供完整 `Driver` 名称和示例配置

### 替换主存储

做法：

1. 实现 `IDataStorageService`
2. 替换宿主层默认注册

### 替换 WAL

做法：

1. 实现 `IWalStorageService`
2. 保持清晰的文件生命周期语义

## 阅读建议

如果你想快速理解代码，推荐这个顺序：

1. `README.md`
2. `docs/design.md`
3. `src/DataAcquisition.Edge.Agent/Program.cs`
4. `src/DataAcquisition.Infrastructure/DataAcquisitions/DataAcquisitionService.cs`
5. `src/DataAcquisition.Infrastructure/Queues/QueueService.cs`
6. `src/DataAcquisition.Infrastructure/Clients/HslStandardPlcDriverProvider.cs`

## 相关文档

- [设计](design.md)
- [配置](tutorial-configuration.md)
- [部署](tutorial-deployment.md)
