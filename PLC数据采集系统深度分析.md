# PLC 数据采集系统深度分析

## 一、系统架构概览

### 1.1 分层架构

```
┌─────────────────────────────────────────────────────────┐
│ DataAcquisition.Gateway (Web层)                        │
│ - Controllers (API/View)                                 │
│ - BackgroundServices (后台任务)                         │
│ - Hubs (SignalR实时推送)                                │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ DataAcquisition.Application (应用层)                    │
│ - Abstractions (接口定义)                               │
│ - 服务契约与业务逻辑                                    │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ DataAcquisition.Infrastructure (基础设施层)              │
│ - Clients (PLC客户端实现)                               │
│ - DataAcquisitions (采集流程)                          │
│ - DataStorages (存储实现)                               │
│ - Queues (消息队列)                                     │
│ - DeviceConfigs (配置管理)                              │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ DataAcquisition.Domain (领域层)                        │
│ - Models (领域模型)                                     │
│ - Clients (客户端模型)                                  │
│ - OperationalEvents (运行事件)                          │
└─────────────────────────────────────────────────────────┘
```

### 1.2 核心数据流

```
PLC设备
  ↓
[HeartbeatMonitor] 心跳检测 → PlcStateManager (连接状态)
  ↓
[ChannelCollector] 通道采集 → 触发条件判断 → 数据读取
  ↓
[DataProcessingService] 表达式计算/数据转换
  ↓
[QueueService] 本地队列缓冲 → 批量聚合
  ↓
[FallbackDataStorageService]
  ├─→ InfluxDB (优先) ✅
  └─→ Parquet (降级) → [ParquetRetryWorker] 后台写回
```

### 1.3 关键组件职责

| 组件 | 职责 | 关键特性 |
|------|------|----------|
| `DataAcquisitionService` | 采集任务编排 | 配置热更新、任务生命周期管理 |
| `ChannelCollector` | 通道级采集 | 条件触发、批量读取、频率控制 |
| `HeartbeatMonitor` | 连接健康监控 | 周期性检测、状态记录 |
| `LocalQueueService` | 消息缓冲 | 批量聚合、定时刷新、失败重试 |
| `FallbackDataStorageService` | 降级存储 | InfluxDB优先，Parquet降级 |
| `ParquetRetryWorker` | 数据写回 | 后台扫描、批量写回、文件清理 |

## 二、命名规范分析

### 2.1 ✅ 良好的命名实践

1. **接口命名清晰**
   - `I*Service`：服务接口（`IDataStorageService`, `IQueueService`）
   - `I*Manager`：管理器接口（`IPlcStateManager`, `IAcquisitionStateManager`）
   - `I*Monitor`：监控器接口（`IHeartbeatMonitor`）
   - `I*Collector`：采集器接口（`IChannelCollector`）

2. **实现类命名规范**
   - 接口实现直接使用类名（`DataAcquisitionService`, `ChannelCollector`）
   - 存储实现后缀明确（`InfluxDbDataStorageService`, `ParquetFileStorageService`）

3. **领域模型命名**
   - `DeviceConfig`：设备配置
   - `DataAcquisitionChannel`：采集通道
   - `DataMessage`：数据消息
   - `AcquisitionTrigger`：采集触发器

### 2.2 ⚠️ 命名不一致问题

1. **命名空间不统一**
   ```csharp
   // 问题：部分类在 Application 层，部分在 Infrastructure 层
   DataAcquisition.Application.PlcStateManager  // 实现类在 Application？
   DataAcquisition.Infrastructure.DataAcquisitions.DataAcquisitionService
   ```
   **建议**：`PlcStateManager` 应移至 `Infrastructure` 层

2. **后缀混用**
   ```csharp
   IPlcClientService      // Service 后缀
   IPlcClientFactory      // Factory 后缀
   ITriggerEvaluator      // Evaluator 后缀（无统一规范）
   ```
   **建议**：统一使用 `Service` 或 `Factory`，`Evaluator` 可改为 `ITriggerEvaluationService`

3. **缩写不一致**
   ```csharp
   PlcStateManager        // Plc 缩写
   IPlcClientService      // Plc 缩写
   DeviceConfig           // 完整单词
   ```
   **建议**：统一使用 `PLC` 或 `Plc`，建议使用 `PLC`（全大写）更符合工业标准

### 2.3 📝 命名改进建议

| 当前命名 | 建议命名 | 理由 |
|---------|---------|------|
| `PlcStateManager` | `PLCStateManager` | 统一缩写规范 |
| `ITriggerEvaluator` | `ITriggerEvaluationService` | 统一服务后缀 |
| `DataAcquisitionService` | `DeviceAcquisitionOrchestrator` | 更明确表达编排职责 |
| `ChannelCollector` | `ChannelAcquisitionService` | 统一服务命名 |

## 三、当前存在的问题

### 3.1 🔴 架构问题

#### 问题1：职责边界不清
- **现象**：`DataAcquisitionService` 既负责任务编排，又负责PLC客户端创建
- **影响**：违反单一职责原则，难以测试和维护
- **建议**：引入 `IPLCClientLifecycleManager` 专门管理客户端生命周期

#### 问题2：状态管理分散
- **现象**：`PlcStateManager` 在 Application 层，但被 Infrastructure 层大量使用
- **影响**：依赖方向错误，Application 不应依赖 Infrastructure
- **建议**：将 `PlcStateManager` 移至 Infrastructure 层，或抽象为接口

#### 问题3：错误处理不统一
- **现象**：各组件错误处理方式不一致，有的抛异常，有的记录日志
- **影响**：难以追踪错误链路，降级策略不明确
- **建议**：统一错误处理策略，引入 `IErrorHandler` 接口

### 3.2 🟡 性能问题

#### 问题1：锁竞争
```csharp
// ChannelCollector.cs:76
await locker.WaitAsync(ct).ConfigureAwait(false);
// 每个通道采集都需要获取锁，高并发时可能成为瓶颈
```
- **影响**：多通道采集时，锁竞争可能导致延迟
- **建议**：考虑读写锁或更细粒度的锁策略

#### 问题2：批量聚合效率
```csharp
// LocalQueueService.cs:209
if (batch.Count >= dataMessage.BatchSize)
// 基于 Measurement 的批量聚合，不同 Measurement 无法合并
```
- **影响**：相同时间窗口的数据无法跨 Measurement 合并
- **建议**：考虑时间窗口批量策略，而非仅基于数量

#### 问题3：Parquet 文件滚动
```csharp
// ParquetFileStorageService.cs:74
await RollIfNeededAsync().ConfigureAwait(false);
// 每次写入都检查滚动，可能频繁创建文件
```
- **影响**：小文件过多，影响后续读取效率
- **建议**：延迟滚动检查，或使用后台任务统一处理

### 3.3 🟢 功能缺失

#### 问题1：数据验证缺失
- **现象**：采集的数据直接写入，无验证机制
- **影响**：异常数据可能污染数据库
- **建议**：引入 `IDataValidator` 接口，支持规则配置

#### 问题2：采集频率控制粗糙
```csharp
// ChannelCollector.cs:158
if (isUnconditionalAcquisition && fireStart && dataAcquisitionChannel.AcquisitionInterval > 0)
    await Task.Delay(dataAcquisitionChannel.AcquisitionInterval, ct).ConfigureAwait(false);
```
- **现象**：仅支持固定间隔，无法动态调整
- **影响**：无法根据系统负载自适应调整
- **建议**：引入频率控制器，支持动态调整

#### 问题3：配置验证不足
```csharp
// DataAcquisitionService.cs:72-88
// 仅检查 null 和空值，未验证配置合理性
```
- **影响**：无效配置可能导致运行时错误
- **建议**：配置加载时进行完整验证

### 3.4 🔵 可维护性问题

#### 问题1：硬编码值
```csharp
// ChannelCollector.cs:111
await Task.Delay(100, ct).ConfigureAwait(false);  // 硬编码延迟
// LocalQueueService.cs:29
private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5);  // 硬编码间隔
```
- **建议**：提取为配置项

#### 问题2：日志信息不统一
- **现象**：日志格式不一致，有的用中文，有的用英文
- **建议**：统一日志格式，使用结构化日志

#### 问题3：缺少单元测试
- **现象**：未发现测试项目
- **建议**：为核心组件添加单元测试和集成测试

## 四、后续改进方向

### 4.1 🎯 短期优化（1-2周）

#### 1. 命名规范化
- [ ] 统一 `PLC` 缩写规范
- [ ] 将 `PlcStateManager` 移至 Infrastructure 层
- [ ] 统一服务接口后缀

#### 2. 配置提取
- [ ] 将硬编码延迟/间隔提取为配置
- [ ] 添加配置验证逻辑
- [ ] 支持环境变量配置

#### 3. 错误处理统一
- [ ] 定义统一错误码
- [ ] 实现错误处理中间件
- [ ] 完善降级策略

### 4.2 🚀 中期改进（1-2月）

#### 1. 性能优化
- [ ] 实现读写锁优化锁竞争
- [ ] 时间窗口批量聚合策略
- [ ] Parquet 文件合并优化

#### 2. 数据质量保障
- [ ] 实现数据验证器
- [ ] 支持验证规则配置
- [ ] 异常数据告警机制

#### 3. 可观测性增强
- [ ] 完善指标收集（采集成功率、数据质量指标）
- [ ] 分布式追踪支持
- [ ] 健康检查端点

### 4.3 🌟 长期规划（3-6月）

#### 1. 架构重构
- [ ] 引入领域驱动设计（DDD）
- [ ] 实现 CQRS 模式（读写分离）
- [ ] 微服务化拆分（采集服务、存储服务、监控服务）

#### 2. 高可用性
- [ ] 多实例部署支持
- [ ] 数据采集任务分布式调度
- [ ] 故障自动恢复机制

#### 3. 扩展性提升
- [ ] 插件化架构（支持自定义采集协议）
- [ ] 规则引擎（支持复杂触发条件）
- [ ] 数据流处理（支持实时计算）

### 4.4 🔮 创新方向

#### 1. 智能化
- [ ] 基于机器学习的异常检测
- [ ] 自适应采集频率调整
- [ ] 预测性维护支持

#### 2. 边缘计算
- [ ] 边缘节点数据预处理
- [ ] 边缘-云端数据同步
- [ ] 离线采集支持

#### 3. 数据治理
- [ ] 数据血缘追踪
- [ ] 数据质量评分
- [ ] 合规性检查

## 五、代码质量评分

| 维度 | 评分 | 说明 |
|------|------|------|
| **架构设计** | 7/10 | 分层清晰，但职责边界需优化 |
| **命名规范** | 6/10 | 基本规范，但存在不一致 |
| **错误处理** | 6/10 | 有处理，但不统一 |
| **性能优化** | 7/10 | 批量处理良好，但锁竞争需优化 |
| **可维护性** | 6/10 | 代码清晰，但缺少测试 |
| **可扩展性** | 8/10 | 接口设计良好，易于扩展 |
| **文档完整性** | 7/10 | README 完善，但缺少架构文档 |

**总体评分：6.7/10** - 良好的基础架构，有明确的改进空间

## 六、总结

### 6.1 优势
1. ✅ **清晰的分层架构**：Domain/Application/Infrastructure/Gateway 分层明确
2. ✅ **良好的接口设计**：易于扩展和替换实现
3. ✅ **降级存储机制**：InfluxDB + Parquet 双重保障
4. ✅ **配置热更新**：支持动态配置变更
5. ✅ **批量处理优化**：减少数据库写入压力

### 6.2 待改进
1. ⚠️ **命名规范统一**：需要统一缩写和服务后缀
2. ⚠️ **职责边界**：部分组件职责过重
3. ⚠️ **测试覆盖**：缺少单元测试和集成测试
4. ⚠️ **性能优化**：锁竞争和批量策略可优化

### 6.3 建议优先级
1. **P0（立即）**：命名规范化、配置提取、错误处理统一
2. **P1（短期）**：性能优化、数据验证、可观测性增强
3. **P2（中期）**：架构重构、高可用性、扩展性提升
4. **P3（长期）**：智能化、边缘计算、数据治理

---

**分析日期**：2025-12-11
**分析范围**：PLC 数据采集核心系统
**分析深度**：架构、命名、问题、改进方向
