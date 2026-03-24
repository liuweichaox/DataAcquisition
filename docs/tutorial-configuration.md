# 配置教程

本文档说明三个层次的配置：

- 设备级配置：如何连接 PLC
- 通道级配置：如何采集和组织数据
- 应用级配置：如何设置主存储、WAL 和运行时行为

## 配置文件位置

- 设备配置：`src/DataAcquisition.Edge.Agent/Configs/*.json`
- 应用配置：`src/DataAcquisition.Edge.Agent/appsettings.json`

## 1. 设备级配置

最小结构：

```json
{
  "SchemaVersion": 1,
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Driver": "melsec-a1e",
  "Host": "192.168.1.100",
  "Port": 502,
  "ProtocolOptions": {
    "connect-timeout-ms": "5000",
    "receive-timeout-ms": "5000"
  },
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": []
}
```

字段说明：

| 字段 | 必填 | 说明 |
|------|:----:|------|
| `SchemaVersion` | ✅ | 当前配置结构版本，当前固定为 `1` |
| `IsEnabled` | ✅ | 是否启用该设备 |
| `PlcCode` | ✅ | 设备唯一编码 |
| `Driver` | ✅ | 稳定驱动名称，例如 `melsec-a1e`、`melsec-mc`、`siemens-s7` |
| `Host` | ✅ | PLC 主机地址，支持 IP 或主机名 |
| `Port` | ✅ | PLC 端口 |
| `ProtocolOptions` | 可选 | 当前驱动支持的附加参数 |
| `HeartbeatMonitorRegister` | ✅ | 心跳寄存器 |
| `HeartbeatPollingInterval` | ✅ | 心跳轮询间隔，毫秒 |
| `Channels` | ✅ | 通道列表 |

注意：

- 驱动配置只接受完整 `Driver` 名称，不接受别名。
- `ProtocolOptions` 不是自由字典。未被当前驱动声明支持的参数会在运行时被拒绝。
- 文档中的驼峰写法如 `cpuType`、`slotNo` 会被兼容解析。
- 当前驱动清单见 [hsl-drivers.md](hsl-drivers.md)。
- JSON Schema 见 [../schemas/device-config.schema.json](../schemas/device-config.schema.json)。
- 示例配置见 [../examples/device-configs](../examples/device-configs)。

例如西门子：

```json
{
  "Driver": "siemens-s7",
  "Host": "192.168.1.20",
  "Port": 102,
  "ProtocolOptions": {
    "plc": "S1200"
  }
}
```

例如汇川：

```json
{
  "Driver": "inovance-tcp",
  "Host": "192.168.1.30",
  "Port": 502,
  "ProtocolOptions": {
    "series": "AM",
    "station": "1"
  }
}
```

## 2. 通道级配置

一个设备可以配置多个通道，每个通道对应一类业务数据或一个 measurement。

示例：

```json
{
  "Measurement": "sensor",
  "ChannelCode": "PLC01C01",
  "EnableBatchRead": true,
  "BatchReadRegister": "D6000",
  "BatchReadLength": 10,
  "BatchSize": 10,
  "AcquisitionInterval": 100,
  "AcquisitionMode": "Always",
  "Metrics": []
}
```

字段说明：

| 字段 | 必填 | 说明 |
|------|:----:|------|
| `Measurement` | ✅ | 主 measurement 名称 |
| `ChannelCode` | ✅ | 通道唯一编码 |
| `EnableBatchRead` | ✅ | 是否批量读取 |
| `BatchReadRegister` | 条件 | 批量读取起始地址 |
| `BatchReadLength` | 条件 | 批量读取长度 |
| `BatchSize` | ✅ | 队列聚合后单批写入大小 |
| `AcquisitionInterval` | ✅ | 采集间隔，`0` 表示不主动延迟 |
| `AcquisitionMode` | ✅ | `Always` 或 `Conditional` |
| `ConditionalAcquisition` | 条件 | 条件采集配置 |
| `Metrics` | 条件 | 指标列表 |

## 3. 指标配置

示例：

```json
{
  "MetricLabel": "temperature",
  "FieldName": "temperature",
  "Register": "D6000",
  "Index": 0,
  "DataType": "short",
  "EvalExpression": "value / 100.0"
}
```

字段说明：

| 字段 | 必填 | 说明 |
|------|:----:|------|
| `MetricLabel` | ✅ | 指标标签 |
| `FieldName` | ✅ | 存储字段名 |
| `Register` | ✅ | PLC 寄存器 |
| `Index` | ✅ | 批量读取缓冲区内偏移 |
| `DataType` | ✅ | 支持的标量类型 |
| `EvalExpression` | 可选 | 数值表达式 |
| `StringByteLength` | 条件 | 字符串字节长度 |
| `Encoding` | 条件 | 字符串编码，建议 `utf-8` |

注意：

- 字符串型固定长度寄存器会自动去除尾部 `\0`
- 表达式只对数值类型应用

## 4. 采集模式

### Always

适合连续信号：

```json
{
  "AcquisitionMode": "Always",
  "AcquisitionInterval": 100
}
```

### Conditional

适合周期、状态切换或事件触发：

```json
{
  "AcquisitionMode": "Conditional",
  "ConditionalAcquisition": {
    "Register": "D6006",
    "DataType": "short",
    "StartTriggerMode": "RisingEdge",
    "EndTriggerMode": "FallingEdge"
  }
}
```

Conditional 模式的关键语义：

- 正式周期事件写入 `Start` / `End`
- 恢复诊断写入 `<measurement>_diagnostic`
- 正式统计时只应使用成对的 `Start` / `End`
- 采集时间统一使用 UTC

## 5. 批量读取

如果寄存器连续，优先启用批量读取：

```json
{
  "EnableBatchRead": true,
  "BatchReadRegister": "D6000",
  "BatchReadLength": 10
}
```

这会减少网络往返和单点读取开销。

## 6. 应用级配置

核心示例：

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
    "DeviceConfigService": {
      "ConfigDirectory": "Configs"
    },
    "StateStore": {
      "DatabasePath": "Data/acquisition-state.db"
    }
  },
  "Edge": {
    "EnableCentralReporting": true,
    "CentralApiBaseUrl": "http://localhost:8000",
    "EdgeId": "EDGE-001",
    "HeartbeatIntervalSeconds": 10
  }
}
```

关键点：

- `Parquet:Directory` 是 WAL 根目录，内部包含 `pending/`、`retry/`、`invalid/`
- `Acquisition:DeviceConfigService:ConfigDirectory` 控制设备配置目录，离线校验默认也读取这里
- `Acquisition:StateStore:DatabasePath` 用于保存 active cycle 恢复状态
- 如果当前只验证 Edge 主链路，可先关闭 `EnableCentralReporting`

## 7. 最佳实践

- 用稳定、可读的 `PlcCode` 和 `ChannelCode`
- 有连续寄存器时优先使用批量读取
- `BatchSize` 以吞吐和延迟之间的平衡为准
- 在采集阶段完成单位换算，而不是把脏原始值留到下游
- 对条件采集，先明确业务上真正需要的开始/结束边沿
- 部署前先执行 `dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs`
- 需要校验其他目录时，可使用 `dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs --config-dir <目录>`

## 下一步

- [部署教程](tutorial-deployment.md)
- [驱动清单](hsl-drivers.md)
- [设计说明](design.md)
