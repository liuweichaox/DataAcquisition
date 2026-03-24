# 入门教程

本文档给出一条最短可运行路径：启动 InfluxDB，使用模拟器生成 PLC 数据，运行 Edge Agent，确认数据进入主存储和 WAL。

## 前置条件

- .NET 10 SDK
- InfluxDB 2.x
- Node.js 20+，仅在使用 Central Web 时需要

默认端口：

| 服务 | 端口 |
|------|------|
| Edge Agent | `8001` |
| Central API | `8000` |
| Central Web | `3000` |
| InfluxDB | `8086` |

## 1. 获取代码

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
```

## 2. 启动 InfluxDB

推荐直接使用仓库内的 Compose 文件：

```bash
docker compose -f docker-compose.tsdb.yml up -d
```

更多说明见 [docker-influxdb.md](docker-influxdb.md)。

## 3. 配置 Edge Agent

编辑 [src/DataAcquisition.Edge.Agent/appsettings.json](../src/DataAcquisition.Edge.Agent/appsettings.json)：

```json
{
  "Urls": "http://+:8001",
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token",
    "Org": "default",
    "Bucket": "iot"
  },
  "Parquet": {
    "Directory": "./Data/parquet"
  },
  "Acquisition": {
    "StateStore": {
      "DatabasePath": "Data/acquisition-state.db"
    }
  },
  "Edge": {
    "EnableCentralReporting": false,
    "CentralApiBaseUrl": "http://localhost:8000",
    "EdgeId": "EDGE-001",
    "HeartbeatIntervalSeconds": 10
  }
}
```

如果当前只验证采集主链路，建议先把 `EnableCentralReporting` 设为 `false`，避免中心侧未启动时的注册重试噪音。

## 4. 启动 PLC 模拟器

```bash
dotnet run --project src/DataAcquisition.Simulator
```

模拟器会持续输出寄存器变化，用于替代真实 PLC。

## 5. 准备设备配置

参考 [src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json](../src/DataAcquisition.Edge.Agent/Configs/TEST_PLC.json)。最小示例：

```json
{
  "SchemaVersion": 1,
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Driver": "melsec-a1e",
  "Host": "127.0.0.1",
  "Port": 502,
  "ProtocolOptions": {
    "connect-timeout-ms": "5000",
    "receive-timeout-ms": "5000"
  },
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "PLC01C01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 10,
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Always",
      "Metrics": [
        {
          "MetricLabel": "temperature",
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        }
      ]
    }
  ]
}
```

驱动名称只接受完整 `Driver` 名称。完整清单见 [hsl-drivers.md](hsl-drivers.md)。
如果编辑器支持 JSON Schema，可以关联 [../schemas/device-config.schema.json](../schemas/device-config.schema.json)。

## 6. 启动 Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

如果设备配置和 InfluxDB 正常，控制台会出现采集和写入日志。

只校验配置而不启动服务：

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

## 7. 验证主存储

在 InfluxDB 中执行：

```flux
from(bucket: "iot")
  |> range(start: -10m)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> yield(name: "latest")
```

如果能看到数据，说明主链路已经跑通。

## 8. 验证 WAL

停止 InfluxDB 后继续运行 Edge Agent，观察：

- `pending/` 可能短暂出现新文件
- 主存储持续失败时，文件会转入 `retry/`
- 如果出现无法写入 WAL 的坏消息，会进入 `invalid/`

默认目录：

- `src/DataAcquisition.Edge.Agent/Data/parquet/pending/`
- `src/DataAcquisition.Edge.Agent/Data/parquet/retry/`
- `src/DataAcquisition.Edge.Agent/Data/parquet/invalid/`

## 9. 可选：启动中心侧

Central 侧不是主采集链路，建议在 Edge 单独跑通后再启用。

启动 Central API：

```bash
dotnet run --project src/DataAcquisition.Central.Api
```

启动 Central Web：

```bash
cd src/DataAcquisition.Central.Web
pnpm install
pnpm run serve
```

## 下一步

- [配置教程](tutorial-configuration.md)
- [部署教程](tutorial-deployment.md)
- [设计说明](design.md)
- [开发扩展教程](tutorial-development.md)
