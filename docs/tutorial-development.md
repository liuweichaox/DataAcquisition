# 开发扩展教程：协议与存储扩展

本教程面向二次开发者，说明如何扩展 PLC 协议与存储后端。

---

## 1. 项目结构

- Application：接口定义与抽象
- Domain：核心模型
- Infrastructure：具体实现（PLC 客户端、存储、队列）
- Edge Agent：采集服务
- Central API/Web：集中管理

---

## 2. 添加新的 PLC 协议

步骤概览：

1. 如需接入新的通讯库，优先继承 `PlcClientServiceBase`
2. 实现 `IPlcDriverProvider`
3. 在 DI 中注册新的 provider
4. 通过完整 `Driver` 名称配置使用该驱动

建议：
- 复用 `HslCommunication` 的协议实现
- 保持连接生命周期与心跳检测一致
- 只实现当前驱动真正需要的小能力：`IPlcConnectionClient`、`IPlcDataAccessClient`、`IPlcTypedWriteClient`
- 对 `Host` / `Port` 和 `ProtocolOptions` 保持显式、诚实的配置契约
- 默认优先实现为 `IPlcDriverProvider`，不要再引入新的硬编码工厂分支

---

## 3. 扩展存储后端

这个项目把“主存储”和 “WAL 存储”明确拆开：

- `IDataStorageService`
  - 负责主存储写入
  - 当前核心方法是 `SaveBatchAsync(List<DataMessage>)`
- `IWalStorageService`
  - 负责本地 WAL 生命周期
  - 需要实现 `WriteAsync` / `ReadAsync` / `DeleteAsync` / `MoveToRetryAsync` / `GetRetryFilesAsync` / `QuarantineInvalidAsync`

扩展建议：

1. 替换主存储时，只改 `IDataStorageService`
2. 替换 WAL 时，只改 `IWalStorageService`
3. 不要绕过 `QueueService` 直接写主存储，否则会破坏 WAL-first 语义
4. 保留 `pending/retry/invalid` 这种状态边界，避免实时写入和后台重试打架

---

## 4. 自定义数据处理

- 使用 `EvalExpression` 进行单位换算
- 可在写入前增加校验与过滤逻辑

---

## 5. 测试建议

- 单元测试：接口行为和边界情况
- 集成测试：实际 PLC 或 Simulator
- 性能测试：采集频率、写入延迟

---

## 6. 贡献流程

1. Fork 项目
2. 创建分支
3. 提交 PR

---

下一步建议阅读：

- [贡献指南](../CONTRIBUTING.md)
- [模块文档](modules.md)
- [设计理念](design.md)
