# 部署

本文说明如何将 DataAcquisition 部署为一个长期运行、可观测、以实时采集为优先的 PLC 数据采集系统。

推荐部署原则如下：

- `Edge Agent` 是采集主程序，必须部署在靠近 PLC 的节点
- `InfluxDB` 是默认 TSDB 实现
- 本地状态只保留日志与条件采集状态，不引入 WAL 或后台回放目录
- `Central API / Central Web` 是可选的中心控制面，不是采集前提

## 推荐部署拓扑

### 单节点

适合本地验证、实验室、单条产线：

- 1 个 `Edge Agent`
- 1 个 `InfluxDB`
- 可选 1 套 `Central API / Central Web`

### 多节点

适合多车间、多产线或多工厂：

- 每个采集节点部署自己的 `Edge Agent`
- 每个采集节点保留自己的日志和条件采集状态库
- 中心侧部署统一的 `Central API / Central Web`
- `InfluxDB` 可以集中部署，也可以按站点拆分

核心约束是：

- `Edge Agent` 必须在 PLC 可达的网络里
- TSDB 可达性比中心侧可达性更关键

## 运行时组件

### Edge Agent

职责：

- 加载设备配置
- 建立 PLC 连接
- 执行 Always / Conditional 采集
- 按批次直接写 InfluxDB
- 暴露本地健康、日志、指标和诊断接口

### InfluxDB

职责：

- 作为默认时序存储实现

### Central API / Central Web

职责：

- 展示节点状态
- 查看心跳
- 聚合指标
- 代理边缘诊断接口

注意：

- 中心侧不可用时，采集主链路仍应继续运行
- 中心侧不是存储成功语义的一部分

## 推荐发布方式

生产环境推荐使用 `dotnet publish` 后的二进制部署，而不是直接在生产环境执行 `dotnet run`。

### 发布 Edge Agent

```bash
dotnet publish src/DataAcquisition.Edge.Agent -c Release -o ./publish/edge
```

启动：

```bash
./publish/edge/DataAcquisition.Edge.Agent
```

### 发布 Central API

```bash
dotnet publish src/DataAcquisition.Central.Api -c Release -o ./publish/central-api
```

启动：

```bash
./publish/central-api/DataAcquisition.Central.Api
```

### 构建 Central Web

```bash
cd src/DataAcquisition.Central.Web
pnpm install
pnpm run build
```

构建输出在 `dist/`，应由 nginx 或其他静态文件服务托管。

## 容器化边界

仓库里的 Compose 文件主要用于：

- `InfluxDB`
- `Central API`
- `Central Web`

不建议把 `Edge Agent` 作为默认容器化部署模型写进主路径。

原因不是“不能容器化”，而是：

- Edge 需要稳定访问 PLC 网络
- 现场通常涉及真实网卡、VLAN、路由和防火墙
- 宿主机进程部署更容易排查网络问题

因此推荐策略是：

- 中心组件可以容器化
- `InfluxDB` 可以容器化
- `Edge Agent` 优先作为宿主机进程部署

## 运行数据目录

生产环境需要重点关注：

- `Data/logs.db`
- `Data/acquisition-state.db`

含义：

- `logs.db`：本地日志存储，默认保留 30 天
- `Logging:RetentionDays`：用于调整本地日志保留天数；设置为 `<= 0` 时关闭清理
- `acquisition-state.db`：条件采集的 active cycle 状态库

这里不保存原始采集数据的本地补偿副本。

## 上线前配置检查

至少确认这些配置：

### 应用级

- `Urls`
- `Logging:DatabasePath`
- `Logging:RetentionDays`
- `InfluxDB:*`
- `Acquisition:DeviceConfigService:ConfigDirectory`
- `Acquisition:StateStore:DatabasePath`
- `Edge:EnableCentralReporting`
- `Edge:CentralApiBaseUrl`

### 设备级

- `PlcCode`
- `Driver`
- `Host`
- `Port`
- `ProtocolOptions`
- `Channels`

上线前建议执行：

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

这是推荐流程的一部分，不是可选技巧。

## 上线后检查

系统启动后，先做这些检查。

### 1. 进程状态

- `Edge Agent` 是否在运行
- `InfluxDB` 是否可访问

### 2. 健康接口

```bash
curl http://localhost:8001/health
```

### 3. 指标接口

```bash
curl http://localhost:8001/metrics
```

### 4. 日志状态

重点检查：

- 是否出现 PLC 连接错误
- 是否出现 TSDB 写入失败
- 配置变更是否被正确加载

### 5. 存储写入

确认 InfluxDB 中已经有对应 measurement。

## 备份策略

如需保留诊断和条件采集上下文，至少备份这两类数据：

### 本地运行状态

- `Data/logs.db`
- `Data/acquisition-state.db`

如果需要更长的本地诊断窗口，应显式调大 `Logging:RetentionDays`，不要默认认为 `logs.db` 会无限增长。

### 存储

- InfluxDB bucket 数据

项目当前不依赖本地原始数据补偿目录，因此备份策略应以 InfluxDB 为主。

## 运维建议

- 使用 `systemd`、Windows Service 或其他服务管理器托管 `Edge Agent`
- 把中心服务和采集服务看作两个独立运行面
- 先保证 `Edge -> InfluxDB` 正常，再考虑中心可视化
- 如果 TSDB 写入失败，应把它视为需要立即处理的运行告警，而不是等待后台补写

## 相关文档

- [快速开始](tutorial-getting-started.md)
- [配置](tutorial-configuration.md)
- [驱动目录](hsl-drivers.md)
