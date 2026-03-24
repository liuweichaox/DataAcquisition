# 设计说明

这份文档说明项目的核心设计取舍，而不是逐文件罗列实现细节。

## 1. 产品边界

DataAcquisition 的核心产品是 `Edge Agent`。

也就是说，项目首先解决的是：

- PLC 怎么连
- 数据怎么采
- 本地怎么兜底
- 主存储失败后怎么恢复

中心侧 `Central API` / `Central Web` 是辅助控制面，用于：

- 节点注册
- 心跳和状态查看
- 指标与日志代理

它不是采集主链路的前置依赖。

## 2. 主数据链路

运行时主链路如下：

```text
PLC -> Collector -> Queue -> Parquet WAL -> Primary Storage
```

详细含义：

1. `Collector`
   按设备配置和通道配置从 PLC 读取数据。

2. `Queue`
   负责消息聚合、批量刷写和失败回补。

3. `Parquet WAL`
   本地可恢复副本。先写本地，再尝试主存储。

4. `Primary Storage`
   当前默认实现是 InfluxDB。

如果主存储失败：

- WAL 文件移动到 `retry/`
- 后台重试任务继续补偿写入

如果某条消息本身写不进 WAL：

- 单条隔离到 `invalid/`
- 不拖死整批健康消息

## 3. 为什么采用 WAL-first

工业现场里，主存储失败是常态之一，不是例外。

例如：

- InfluxDB 未启动
- 网络短暂断开
- 目标端口不可达

如果采集链路直接写主存储，边缘节点会在这类场景下直接丢数。  
因此项目采用：

- 先写本地 WAL
- 再写主存储

这样主存储失败时，边缘节点仍然保留可恢复副本。

## 4. 配置模型

项目使用 JSON 设备配置，而不是数据库配置中心。

原因：

- 边缘节点部署简单
- 文件易于版本管理
- 与 .NET 配置系统天然兼容
- 更适合工业现场单机运维

配置结构的核心字段是：

- `Driver`
- `Host`
- `Port`
- `ProtocolOptions`
- `Channels`

设计原则：

- 顶层字段保持稳定
- 协议差异下沉到 `ProtocolOptions`
- 配置先校验，再运行

## 5. 驱动模型

驱动选择使用稳定的 `Driver` 名称，例如：

- `melsec-a1e`
- `melsec-mc`
- `siemens-s7`

框架核心不直接依赖具体 PLC 库，而是通过：

- `IPlcDriverProvider`
- `IPlcClientService`

建立协议扩展点。

默认内置实现当前基于 HslCommunication，但这只是默认实现，不是架构前提。

## 6. 采集模式

项目支持两种采集模式：

### Always

适合连续量、状态量、实时信号。

### Conditional

适合周期、工步、事件边沿。

Conditional 模式下：

- 正式业务事件写入 `Start` / `End`
- 恢复诊断写入 `<measurement>_diagnostic`

这样可以避免恢复语义污染正式周期统计。

## 7. 时间语义

系统内部统一使用 UTC 时间。

原因：

- 多 Edge 节点时间可比较
- WAL 回放和重试有唯一时间语义
- 避免本地时区和夏令时歧义

展示层如果需要本地时间，应在 UI 或查询层再做转换。

## 8. 状态恢复

条件采集不仅依赖当前寄存器值，也依赖 active cycle 状态。

为了应对进程重启，项目将 active cycle 做成：

- 内存热状态
- SQLite 本地恢复镜像

因此服务重启后：

- 不会完全丢失 active cycle 上下文
- 能写出恢复诊断事件

## 9. 代码分层

当前采用的分层是：

- `Domain`
  领域模型、消息模型、配置模型
- `Application`
  运行时抽象和契约
- `Infrastructure`
  驱动、采集、存储、WAL、日志、指标实现
- `Edge.Agent`
  主运行时入口
- `Central.Api` / `Central.Web`
  中心侧辅助组件

设计目标不是“纯学术分层”，而是：

- 让默认实现集中在 Infrastructure
- 让扩展点稳定留在 Application
- 让部署入口保持简单

## 10. 设计原则

这个项目目前坚持的原则是：

- `Edge First`
- `WAL First`
- `Configuration Before Runtime`
- `Explicit Driver Contracts`
- `Formal Events Separate From Diagnostics`

这些原则比“某个类拆成几个文件”更重要。

## 11. 当前演进方向

当前架构已经稳定，后续优化优先级主要是：

1. 增加更多真实驱动示例配置
2. 增加更多端到端测试
3. 继续完善主流驱动的 `ProtocolOptions`
4. 强化故障排查和运维文档
