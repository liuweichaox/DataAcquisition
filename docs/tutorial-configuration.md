# 配置说明

本项目的配置目标不是“把所有协议差异揉成一个万能模型”，而是：

- 顶层结构稳定
- 驱动选择明确
- 驱动私有差异放进 `ProtocolOptions`
- 配置先校验，再运行

## 配置文件在哪里

设备配置默认目录：

- [src/DataAcquisition.Edge.Agent/Configs](../src/DataAcquisition.Edge.Agent/Configs)

应用配置：

- [src/DataAcquisition.Edge.Agent/appsettings.json](../src/DataAcquisition.Edge.Agent/appsettings.json)

离线校验入口：

```bash
dotnet run --project src/DataAcquisition.Edge.Agent -- --validate-configs
```

JSON Schema：

- [../schemas/device-config.schema.json](../schemas/device-config.schema.json)

示例配置：

- [../examples/device-configs](../examples/device-configs)

## 1. 设备配置结构

最小示例：

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
  "Channels": []
}
```

字段说明：

| 字段 | 必填 | 说明 |
|------|:----:|------|
| `SchemaVersion` | ✅ | 配置结构版本，当前固定为 `1` |
| `IsEnabled` | ✅ | 是否启用该设备 |
| `PlcCode` | ✅ | 设备唯一编码，不能和其他文件重复 |
| `Driver` | ✅ | 稳定驱动名称，例如 `melsec-a1e`、`siemens-s7` |
| `Host` | ✅ | PLC 主机地址，支持 IP 或主机名 |
| `Port` | ✅ | PLC 端口 |
| `ProtocolOptions` | 可选 | 驱动附加参数 |
| `HeartbeatMonitorRegister` | ✅ | 心跳寄存器 |
| `HeartbeatPollingInterval` | ✅ | 心跳轮询间隔，毫秒 |
| `Channels` | ✅ | 通道列表 |

规则：

- `Driver` 只接受完整名称，不接受别名
- `ProtocolOptions` 不是自由字典，未声明支持的键会被拒绝
- `PlcCode` 在配置目录内必须唯一

## 2. 通道配置

一个设备下可以配置多个通道。每个通道通常对应一个 measurement。

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
| `Measurement` | ✅ | 写入主存储的 measurement |
| `ChannelCode` | ✅ | 通道编码 |
| `EnableBatchRead` | ✅ | 是否启用批量读取 |
| `BatchReadRegister` | 条件 | 批量读取起始地址 |
| `BatchReadLength` | 条件 | 批量读取长度 |
| `BatchSize` | ✅ | 队列聚合大小 |
| `AcquisitionInterval` | ✅ | 采集间隔，毫秒 |
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
| `MetricLabel` | ✅ | 可读标签 |
| `FieldName` | ✅ | 存储字段名 |
| `Register` | ✅ | PLC 地址 |
| `Index` | ✅ | 批量读取缓冲区偏移 |
| `DataType` | ✅ | 数据类型 |
| `EvalExpression` | 可选 | 表达式计算 |
| `StringByteLength` | 条件 | 字符串字节长度 |
| `Encoding` | 条件 | 字符串编码，建议 `utf-8` |

说明：

- 固定长度字符串会自动去除尾部 `\0`
- 表达式仅对数值类型生效

## 4. 采集模式

### Always

适合连续信号、实时量：

```json
{
  "AcquisitionMode": "Always",
  "AcquisitionInterval": 100
}
```

### Conditional

适合周期开始/结束、事件触发：

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

Conditional 模式语义：

- 正式业务事件写为 `Start` / `End`
- 恢复诊断写入 `<measurement>_diagnostic`
- 正式统计只应基于成对的 `Start` / `End`

## 5. `ProtocolOptions`

`ProtocolOptions` 是驱动的附加参数区。

通用键：

- `connect-timeout-ms`
- `receive-timeout-ms`

部分驱动还有专属键，例如：

- `siemens-s7` 使用 `plc`
- `inovance-tcp` 使用 `series`、`station`
- `lsis-fast-enet` 使用 `cpu-type`、`slot-no`

完整清单见：

- [hsl-drivers.md](hsl-drivers.md)

## 6. 配置目录

默认设备配置目录来自应用配置：

```json
{
  "Acquisition": {
    "DeviceConfigService": {
      "ConfigDirectory": "Configs"
    }
  }
}
```

规则：

- 相对路径基于应用运行目录解析
- 离线校验默认使用同一个目录
- 可用 `--config-dir` 临时覆盖

## 7. 最佳实践

- `PlcCode`、`ChannelCode` 使用稳定、可读、可搜索的命名
- 连续寄存器优先批量读取
- 在采集阶段做基础单位换算，不把脏原始值留给下游
- 在上线前先执行配置校验
- 不要把驱动不支持的私有参数硬塞进 `ProtocolOptions`

## 下一步

- [快速开始](tutorial-getting-started.md)
- [驱动目录](hsl-drivers.md)
- [部署说明](tutorial-deployment.md)
