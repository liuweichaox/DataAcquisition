# 文档首页

这套文档只围绕一个目标组织：让你把 DataAcquisition 作为一个 PLC 数据采集运行时真正跑起来、部署起来、理解清楚并且可以扩展。

如果你把它当成“很多零散说明页的集合”，阅读体验会很差。  
更好的方式是按目标阅读。

## 先读什么

如果你第一次接触这个项目，按这个顺序读：

1. [快速开始](tutorial-getting-started.md)
2. [配置](tutorial-configuration.md)
3. [驱动目录](hsl-drivers.md)
4. [部署](tutorial-deployment.md)

## 按目标阅读

### 我只想先跑起来

- [快速开始](tutorial-getting-started.md)
- [配置](tutorial-configuration.md)

### 我要接真实 PLC

- [配置](tutorial-configuration.md)
- [驱动目录](hsl-drivers.md)
- [常见问题](faq.md)

### 我要部署到现场

- [部署](tutorial-deployment.md)
- [常见问题](faq.md)

### 我要理解架构

- [设计](design.md)
- [模块](modules.md)

### 我要扩展项目

- [开发扩展](tutorial-development.md)
- [贡献指南](../CONTRIBUTING.md)

## 核心约定

在阅读和使用项目时，先记住这几条：

- `Edge Agent` 是主产品
- `Central` 是可选控制面
- 主链路是 `PLC -> Collector -> Queue -> Parquet WAL -> Primary Storage`
- 驱动由稳定的 `Driver` 名称选择
- 配置必须先校验，再运行
- 正式业务事件和恢复诊断事件分开写入

## 当前文档集

当前文档树只保留核心文档：

- [快速开始](tutorial-getting-started.md)
- [配置](tutorial-configuration.md)
- [驱动目录](hsl-drivers.md)
- [部署](tutorial-deployment.md)
- [设计](design.md)
- [模块](modules.md)
- [开发扩展](tutorial-development.md)
- [常见问题](faq.md)
