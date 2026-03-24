# 部署

这份文档只回答一个问题：如何把 DataAcquisition 部署成一个长期运行、可恢复、可观察的 PLC 数据采集系统。

DataAcquisition 的推荐部署原则很简单：

- `Edge Agent` 是采集主程序，必须部署在靠近 PLC 的节点
- `InfluxDB` 是默认主存储
- `Parquet WAL` 必须保留在边缘节点本地磁盘
- `Central API / Central Web` 是可选的中心控制面，不是采集前提

## 推荐拓扑

### 单节点

适合本地验证、实验室、单条产线：

- 1 个 `Edge Agent`
- 1 个 `InfluxDB`
- 可选 1 套 `Central API / Central Web`

### 多节点

适合多车间、多产线或多工厂：

- 每个采集节点部署自己的 `Edge Agent`
- 每个采集节点保留自己的 `WAL` 和本地状态库
- 中心侧部署统一的 `Central API / Central Web`
- `InfluxDB` 可以集中部署，也可以按站点拆分

核心约束是：

- `Edge Agent` 必须在 PLC 可达的网络里
- `WAL` 不能依赖中心服务或远程共享目录

## 运行时组件

### Edge Agent

职责：

- 加载设备配置
- 建立 PLC 连接
- 执行 Always / Conditional 采集
- 先写 WAL，再写主存储
- 暴露本地健康、日志、指标和诊断接口

### InfluxDB

职责：

- 作为默认时序主存储

### Central API / Central Web

职责：

- 展示节点状态
- 查看心跳
- 聚合指标
- 代理边缘诊断接口

注意：

- 中心侧不可用时，采集主链路仍应继续运行
- 中心侧不是 WAL 或恢复机制的一部分

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

生产环境最重要的不是发布目录，而是运行数据目录。

默认需要重点关注：

- `Data/parquet/pending`
- `Data/parquet/retry`
- `Data/parquet/invalid`
- `Data/logs.db`
- `Data/acquisition-state.db`

含义：

- `pending/`：WAL 刚落盘、尚未完成主存储判定
- `retry/`：主存储失败后等待后台重放
- `invalid/`：坏消息隔离区
- `logs.db`：本地日志存储
- `acquisition-state.db`：条件采集的 active cycle 状态库

如果你在生产环境只盯 `pending/`，很容易误判。真正需要长期关注的是：

- `retry/` 是否持续增长
- `invalid/` 是否出现文件

## 上线前配置检查

至少确认这些配置：

### 应用级

- `Urls`
- `InfluxDB:*`
- `Parquet:Directory`
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

### 4. WAL 状态

重点检查：

- `retry/` 是否持续增长
- `invalid/` 是否出现新文件

### 5. 主存储写入

确认 InfluxDB 中已经有对应 measurement。

## 备份策略

至少备份两类数据。

### 运行数据

- `Data/parquet/`
- `Data/logs.db`
- `Data/acquisition-state.db`

### 主存储

- InfluxDB bucket 数据

如果场景要求强恢复能力，WAL 所在目录不应和系统临时目录混用。

## 运维建议

- 使用 `systemd`、Windows Service 或其他服务管理器托管 `Edge Agent`
- 把 `WAL` 放在本地可靠磁盘，不要放到不稳定网络盘
- 把中心服务和采集服务看作两个独立运行面
- 先保证 `Edge -> WAL -> InfluxDB` 正常，再考虑中心可视化

## 下一步

- [快速开始](tutorial-getting-started.md)
- [配置](tutorial-configuration.md)
- [驱动目录](hsl-drivers.md)
