# 事件总线架构简化方案

## 优化目标

去掉不必要的中间层（Channel + BackgroundService），使用更直接、清晰的实现方式。

## 架构对比

### 旧架构（复杂）

```
OperationalEventsService
  └─ IOpsEventBus
       └─ OpsEventChannel (Channel<OpsEvent>)
            └─ OpsEventDispatcher (BackgroundService)
                 ├─ LoggingEventSubscriber
                 └─ SignalREventSubscriber
```

**问题**：
- ❌ 需要额外的 Channel 缓冲层
- ❌ 需要 BackgroundService 后台服务
- ❌ 依赖关系复杂（3 层）
- ❌ 代码量大，维护成本高

### 新架构（简洁）

```
OperationalEventsService
  └─ IOpsEventBus (InMemoryOpsEventBus)
       ├─ LoggingEventSubscriber
       └─ SignalREventSubscriber
```

**优势**：
- ✅ 直接调用，无需中间层
- ✅ 无需后台服务
- ✅ 依赖关系简单（2 层）
- ✅ 代码简洁，易于理解

## 实现细节

### InMemoryOpsEventBus

```csharp
public sealed class InMemoryOpsEventBus : IOpsEventBus
{
    private readonly IReadOnlyList<IOpsEventSubscriber> _subscribers;

    public ValueTask PublishAsync(OpsEvent evt, CancellationToken ct = default)
    {
        // Fire-and-Forget：不阻塞发布者
        _ = Task.Run(async () =>
        {
            var tasks = _subscribers.Select(subscriber =>
                HandleWithErrorIsolationAsync(subscriber, evt, ct));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }, ct);

        return ValueTask.CompletedTask;
    }
}
```

### 关键特性

1. **Fire-and-Forget 策略**
   - 不等待订阅者完成，立即返回
   - 避免阻塞发布者
   - 后台并行处理所有订阅者

2. **错误隔离**
   - 单个订阅者失败不影响其他订阅者
   - 异常被捕获并记录日志
   - 保证系统稳定性

3. **性能优化**
   - 并行处理所有订阅者
   - 无额外缓冲层开销
   - 直接内存调用，延迟更低

## 性能对比

| 特性 | 旧架构（Channel） | 新架构（直接调用） |
|------|------------------|-------------------|
| 延迟 | 较高（需要经过 Channel） | 低（直接调用） |
| 内存 | 需要缓冲队列 | 无需缓冲 |
| CPU | 需要后台服务线程 | 使用线程池 |
| 代码复杂度 | 高（3 个类） | 低（1 个类） |
| 依赖关系 | 复杂 | 简单 |

## 使用方式

### 配置（Program.cs）

```csharp
// 注册事件订阅者
builder.Services.AddSingleton<IOpsEventSubscriber, LoggingEventSubscriber>();
builder.Services.AddSingleton<IOpsEventSubscriber, SignalREventSubscriber>();

// 注册事件总线（自动注入所有订阅者）
builder.Services.AddSingleton<IOpsEventBus, InMemoryOpsEventBus>();
builder.Services.AddSingleton<IOperationalEventsService, OperationalEventsService>();
```

### 使用（无需改变）

```csharp
// 使用方式完全不变
await _events.InfoAsync("系统启动成功");
await _events.ErrorAsync("连接失败", ex);
```

## 何时使用 Channel

Channel 适用于以下场景：

1. **高吞吐量场景**
   - 事件发布速率远高于消费速率
   - 需要缓冲大量事件

2. **解耦发布和消费**
   - 发布者完全不需要等待
   - 消费速度不固定

3. **批量处理**
   - 需要将事件批量处理
   - 需要按时间窗口聚合

**对于当前的运行事件系统**：
- ✅ 事件频率适中（不是高频）
- ✅ 订阅者处理速度快（日志 + SignalR）
- ✅ 不需要批量处理
- ✅ **不需要 Channel**

## 代码减少

- **删除**：`OpsEventChannel.cs` (27 行)
- **删除**：`OpsEventDispatcher.cs` (129 行)
- **新增**：`InMemoryOpsEventBus.cs` (75 行)
- **净减少**：约 81 行代码

## 总结

新架构的优势：

1. ✅ **更简洁**：去掉不必要的中间层
2. ✅ **更高效**：直接调用，延迟更低
3. ✅ **更易理解**：依赖关系清晰
4. ✅ **更易维护**：代码量减少，复杂度降低
5. ✅ **性能更好**：无额外缓冲和线程开销

符合"不过度设计"的原则，使用最简单有效的方案。
