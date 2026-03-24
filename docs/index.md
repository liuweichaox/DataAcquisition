# 文档首页

这套文档围绕一个目标组织：让你把一个 PLC 数据采集节点真正跑起来，并且知道它的边界、配置方式和扩展方式。

## 先读什么

如果你是第一次接触这个项目，按下面顺序读：

1. [快速开始](tutorial-getting-started.md)
2. [配置说明](tutorial-configuration.md)
3. [驱动目录](hsl-drivers.md)
4. [部署说明](tutorial-deployment.md)

## 按角色阅读

### 我只想先跑起来

- [快速开始](tutorial-getting-started.md)
- [配置说明](tutorial-configuration.md)

### 我要接真实 PLC

- [配置说明](tutorial-configuration.md)
- [驱动目录](hsl-drivers.md)
- [常见问题](faq.md)

### 我要部署到现场

- [部署说明](tutorial-deployment.md)
- [数据流](data-flow.md)
- [常见问题](faq.md)

### 我要理解设计

- [设计说明](design.md)
- [模块划分](modules.md)
- [数据流](data-flow.md)

### 我要扩展项目

- [开发扩展](tutorial-development.md)
- [贡献指南](../CONTRIBUTING.md)

## 参考文档

- [API 使用](api-usage.md)
- [数据查询](tutorial-data-query.md)
- [性能说明](performance.md)
- [Docker 启动 InfluxDB](docker-influxdb.md)

## 设计约定

在阅读和使用这个项目时，可以先记住这几个约定：

- `Edge Agent` 是主产品，`Central` 是辅助控制面
- 主链路是 `PLC -> Collector -> Queue -> Parquet WAL -> Primary Storage`
- 驱动通过稳定的 `Driver` 名称选择
- 配置先校验，再运行
- 正式业务事件和恢复诊断事件分开写入
