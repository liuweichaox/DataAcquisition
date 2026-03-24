# 常见问题

这份 FAQ 只回答高频问题，不重复教程内容。

如果你是第一次接触项目，先看：

- [文档首页](index.md)
- [快速开始](tutorial-getting-started.md)
- [配置](tutorial-configuration.md)

## 项目定位

### DataAcquisition 是什么

它是一个 PLC 数据采集运行时。

它负责：

- 从 PLC 读取数据
- 生成统一的采集消息
- 先写本地 WAL
- 再写主存储
- 暴露本地诊断接口

### DataAcquisition 不是什么

它不是：

- PLC 编程工具
- SCADA 系统
- MES
- 历史数据库本身

中心侧页面是辅助控制面，不是采集主链路本身。

## 配置与驱动

### 我应该使用哪个驱动名

使用 [驱动目录](hsl-drivers.md) 中列出的完整 `Driver` 名称。

不要使用旧式别名、缩写或自己猜的名称。

### 为什么配置校验失败

常见原因：

- JSON 格式不合法
- 缺少必填字段
- `PlcCode` 为空
- `PlcCode` 在多个配置文件中重复
- `Driver` 不在内置目录里
- `ProtocolOptions` 包含当前驱动不支持的键

建议先执行：

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

### 修改配置后需要重启吗

通常不需要。

设备配置目录由文件监视器监听，配置变更后会自动重新加载。

但有一个前提：

- 新配置必须先通过校验

### 如何添加新的 PLC 协议

如果内置目录里没有你要的协议，需要新增 provider。

推荐扩展方式：

1. 实现新的 `IPlcDriverProvider`
2. 复用 `PlcClientServiceBase` 或提供自己的 `IPlcClientService`
3. 在启动时注册 provider
4. 为新的 `Driver` 写文档和示例配置

如果只是使用内置 Hsl 驱动，通常不需要改核心代码。

## 采集与存储

### 为什么要先写 WAL

因为主存储可能失败，边缘采集不能直接依赖 InfluxDB 的即时可用性。

WAL-first 的意义是：

- 先把数据安全地落到本地磁盘
- 再尝试写主存储
- 主存储失败后仍可以重放

### `pending`、`retry`、`invalid` 有什么区别

- `pending/`：刚写入的 WAL，尚未完成主存储判定
- `retry/`：主存储失败后等待后台重放
- `invalid/`：无法写入 WAL 的坏消息

这三个目录不是重复设计，而是明确区分文件生命周期和线程职责。

### WAL 文件很多，说明什么

通常说明主存储写入失败或持续不可达。

排查顺序：

1. 看 `retry/` 是否持续增长
2. 检查 InfluxDB 是否可访问
3. 查看 Edge 日志中的写入错误
4. 确认配置的 `InfluxDB:Url`、bucket、org、token 正确

### `invalid/` 里有文件说明什么

说明存在坏消息，系统无法把它写进 WAL。

这类消息已经被隔离，不会继续阻塞正常消息。

应该做的事是：

- 查看对应错误日志
- 找到产生坏消息的字段或配置
- 修复源头，而不是直接忽略

### InfluxDB 挂了会不会影响采集

会影响主存储写入，但不应立即影响采集主链路。

正常预期是：

- 新数据继续写本地 WAL
- `retry/` 堆积
- InfluxDB 恢复后由后台重放

如果 InfluxDB 不可用时连 WAL 也没写进去，那就是异常，不是预期行为。

## 周期采集

### 条件采集的第一次读取会不会误触发

不会。

当前行为是：

- 首拍只建立基线
- 不会把初始化状态当成真实边沿

### 为什么会看到 `RecoveredStart` 或 `Interrupted`

这代表系统在周期进行中发生过重启或恢复。

它们是恢复诊断事件，不应直接当作正式业务周期统计口径。

正式周期统计时，仍应以成对的 `Start` / `End` 为准。

### 为什么还要保存 active cycle 状态

因为条件采集在进程重启后需要恢复上下文。

当前 active cycle 会同时保存在：

- 内存
- `Data/acquisition-state.db`

这不是为了“补造周期”，而是为了让系统知道重启前是否存在未结束周期。

## 运行与排障

### 怎么确认系统是否在正常运行

至少检查这几项：

```bash
curl http://localhost:8001/health
curl http://localhost:8001/metrics
```

再检查：

- `retry/` 是否持续增长
- `invalid/` 是否出现文件
- InfluxDB 是否有 measurement 写入

### 为什么推荐 Edge 用宿主机进程部署

因为现场 PLC 网络通常比 Web 服务更接近真实网络环境问题。

宿主机进程部署更容易定位：

- 网卡选择
- 路由
- VLAN
- 防火墙
- PLC 可达性

中心组件和 InfluxDB 更适合容器化。

### 中心服务挂了会怎样

中心侧不可用时：

- 节点注册和心跳上报会失败
- 中心页面不可用

但这不应该成为采集主链路的停止条件。

## 扩展与开发

### 如何替换主存储

实现 `IDataStorageService`，然后在宿主层替换默认注册。

### 如何替换 WAL

实现 `IWalStorageService`，并保留清晰的生命周期语义。

至少要能表达：

- 新写入
- 等待重放
- 坏消息隔离

### 为什么项目继续使用 JSON 配置

因为这里的目标是：

- 简单
- 可读
- 易于热更新
- 易于在 .NET 环境中校验和绑定

比“换成 YAML/TOML”更重要的是：

- 有稳定的配置契约
- 有校验
- 有示例
- 有清晰错误提示

## 相关文档

- [配置](tutorial-configuration.md)
- [部署](tutorial-deployment.md)
- [驱动目录](hsl-drivers.md)
- [设计](design.md)
