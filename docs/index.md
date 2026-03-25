# 文档首页

本文档集用于说明 DataAcquisition 的配置、部署、运行、设计与扩展方式。

为便于快速定位所需信息，建议按使用目标组织阅读，而不是将其视为彼此独立的零散说明页。

## 建议阅读顺序

如果你第一次接触这个项目，按这个顺序读：

1. [快速开始](tutorial-getting-started.md)
2. [配置](tutorial-configuration.md)
3. [驱动目录](hsl-drivers.md)
4. [部署](tutorial-deployment.md)

## 按使用目标阅读

### 本地启动与联调

- [快速开始](tutorial-getting-started.md)
- [配置](tutorial-configuration.md)

### 接入真实 PLC

- [配置](tutorial-configuration.md)
- [驱动目录](hsl-drivers.md)
- [常见问题](faq.md)

### 现场部署与运维

- [部署](tutorial-deployment.md)
- [常见问题](faq.md)

### 架构与模块理解

- [设计](design.md)
- [模块](modules.md)

### 扩展与贡献

- [开发扩展](tutorial-development.md)
- [贡献指南](../CONTRIBUTING.md)

## 核心约束

在阅读和使用项目时，先记住这几条：

- `Edge Agent` 是主产品
- `Central` 是可选控制面
- 主链路是 `PLC -> Collector -> Queue -> TSDB`
- 队列批次直接写存储，不做本地 WAL 或后台回放
- 驱动由稳定的 `Driver` 名称选择
- 配置必须先校验，再运行
- 正式业务事件和恢复诊断事件分开写入

## 文档清单

当前文档树只保留核心文档：

- [快速开始](tutorial-getting-started.md)
- [配置](tutorial-configuration.md)
- [驱动目录](hsl-drivers.md)
- [部署](tutorial-deployment.md)
- [设计](design.md)
- [模块](modules.md)
- [开发扩展](tutorial-development.md)
- [常见问题](faq.md)
