# 快速开始

这份教程的目标很简单：让你在本地把一个 Edge Agent 跑起来，并确认它能读取配置、启动采集主链路并写入主存储。

## 前置要求

- .NET 10 SDK
- Docker
- InfluxDB 2.x

如果你只是想先验证配置，不需要先准备 PLC。

## 第一步：构建项目

在仓库根目录执行：

```bash
dotnet build DataAcquisition.sln
```

## 第二步：启动 InfluxDB

项目附带了一个简单的 compose 文件：

```bash
docker compose -f docker-compose.tsdb.yml up -d
```

如果你已经有自己的 InfluxDB，只需要确认 [appsettings.json](../src/DataAcquisition.Edge.Agent/appsettings.json) 里的 `InfluxDB` 配置正确即可。

## 第三步：检查设备配置

默认设备配置目录是：

- [src/DataAcquisition.Edge.Agent/Configs](../src/DataAcquisition.Edge.Agent/Configs)

仓库里已经带了一个本地联调示例：

- [TEST_PLC.json](../src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json)

你也可以参考：

- [examples/device-configs](../examples/device-configs)
- [device-config.schema.json](../schemas/device-config.schema.json)

## 第四步：离线校验配置

先确认配置本身是合法的：

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

如果你要校验其他目录：

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs --config-dir ./examples/device-configs
```

校验通过后，你应该能看到类似输出：

```text
[OK] .../TEST_PLC.json (TEST_PLC)
```

## 第五步：启动 Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

默认端口来自 [appsettings.json](../src/DataAcquisition.Edge.Agent/appsettings.json) 中的 `Urls`，默认是：

- `http://localhost:8001`

启动后常用端点：

- `/health`
- `/metrics`
- `/api/logs`
- `/api/DataAcquisition/plc-connections`

## 可选：启动 PLC 模拟器

如果你想在本地做闭环联调，可以启动模拟器：

```bash
dotnet run --project src/DataAcquisition.Simulator
```

模拟器默认监听 `502` 端口，并输出当前寄存器变化。详细说明见：

- [src/DataAcquisition.Simulator/README.md](../src/DataAcquisition.Simulator/README.md)

## 如何确认系统在工作

你可以从这几个角度确认：

### 1. Agent 存活

```bash
curl http://localhost:8001/health
```

### 2. 配置被成功加载

Agent 启动日志里应能看到配置校验成功和 PLC/通道启动相关信息。

### 3. WAL 目录创建成功

默认 WAL 根目录：

- `src/DataAcquisition.Edge.Agent/bin/Debug/net10.0/Data/parquet`

内部状态目录：

- `pending/`
- `retry/`
- `invalid/`

说明：

- `pending/` 是新写入 WAL 的中间态
- `retry/` 是主存储失败后的待补偿文件
- `invalid/` 是无法写入 WAL 的坏消息审计区

### 4. InfluxDB 有数据写入

如果 InfluxDB 可达，WAL 文件会很快被删除，不会长期堆积在 `retry/`。

## 可选：启动中心侧

中心侧不是采集主链路的前置条件，但如果你要看注册、心跳和 Web 界面，可以继续启动：

```bash
dotnet run --project src/DataAcquisition.Central.Api
```

如果要运行 Web：

```bash
cd src/DataAcquisition.Central.Web
pnpm install
pnpm run serve
```

## 常见问题

### 配置校验失败

优先检查：

- `Driver` 是否为完整稳定名称
- `ProtocolOptions` 是否包含当前驱动不支持的键
- `PlcCode` 是否在多个文件中重复

### WAL 一直进 retry

这通常说明主存储不可达，例如 InfluxDB 地址不对或服务未启动。

### 本地模拟器能连，现场 PLC 不能连

优先检查：

- `Host` 和 `Port`
- 现场网络连通性
- 驱动选择是否正确

## 下一步

- [配置说明](tutorial-configuration.md)
- [驱动目录](hsl-drivers.md)
- [部署说明](tutorial-deployment.md)
