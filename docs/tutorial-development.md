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

1. 实现 `IPlcClientService`
2. 在工厂类注册新实现
3. 添加新的 `PlcType` 枚举值
4. 编写连接、读取、写入逻辑

建议：
- 复用 `HslCommunication` 的协议实现
- 保持连接生命周期与心跳检测一致

---

## 3. 扩展存储后端

实现 `IDataStorageService`：

- `WriteAsync`：批量写入
- `InitializeAsync`：初始化连接
- `Dispose`：释放资源

可选方向：TimescaleDB、Kafka、S3 等。

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

下一步建议阅读：[模块文档](modules.md) 和 [设计理念](design.md)
