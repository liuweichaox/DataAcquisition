# ⚙️ 配置说明

本文档详细说明 DataAcquisition 系统的各项配置。

## 相关文档

- [快速开始指南](getting-started.md) - 从零开始使用系统

## 设备配置文件

设备配置文件位于 `src/DataAcquisition.Edge.Agent/Configs/` 目录下，每个 PLC 设备对应一个 JSON 配置文件。

### 设备配置文件示例

以下是一个实际的配置示例（基于项目中的 `TEST_PLC.json`）：

```json
{
  "IsEnabled": true,
  "PlcCode": "TEST_PLC",
  "Host": "127.0.0.1",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 2000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "CH01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 14,
      "BatchSize": 10,
      "AcquisitionInterval": 0,
      "AcquisitionMode": "Always",
      "Metrics": [
        {
          "MetricName": "temperature",
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        },
        {
          "MetricName": "pressure",
          "FieldName": "pressure",
          "Register": "D6001",
          "Index": 2,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        }
      ]
    },
    {
      "Measurement": "production",
      "ChannelCode": "CH01",
      "EnableBatchRead": false,
      "BatchReadRegister": null,
      "BatchReadLength": 0,
      "BatchSize": 1,
      "AcquisitionInterval": 0,
      "AcquisitionMode": "Conditional",
      "Metrics": null,
      "ConditionalAcquisition": {
        "Register": "D6006",
        "DataType": "short",
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

**配置说明：**
- 第一个通道使用 `Always` 模式持续采集传感器数据
- 第二个通道使用 `Conditional` 模式，根据生产序号的变化触发采集
- `AcquisitionInterval` 为 0 表示最高频率采集（无延迟）
- 条件采集模式下 `Metrics` 可以为 `null`

### 设备配置属性详细说明

#### 根级别属性

| 属性名称                   | 类型      | 必填 | 说明                                      |
| -------------------------- | --------- | ---- | ----------------------------------------- |
| `IsEnabled`                | `boolean` | 是   | 设备是否启用                              |
| `PlcCode`                  | `string`  | 是   | PLC 设备的唯一标识符                      |
| `Host`                     | `string`  | 是   | PLC 设备的 IP 地址                        |
| `Port`                     | `integer` | 是   | PLC 设备的通信端口                        |
| `Type`                     | `string`  | 是   | PLC 设备类型（Mitsubishi、Inovance、BeckhoffAds） |
| `HeartbeatMonitorRegister` | `string`  | 否   | 用于监控 PLC 心跳的寄存器地址             |
| `HeartbeatPollingInterval` | `integer` | 否   | 心跳监控的轮询间隔（毫秒）                |
| `Channels`                 | `array`   | 是   | 数据采集通道配置列表                      |

#### Channels 数组属性

| 属性名称                 | 类型      | 必填 | 说明                                                       |
| ------------------------ | --------- | ---- | ---------------------------------------------------------- |
| `Measurement`            | `string`  | 是   | 时序数据库中的测量名称（表名）                             |
| `ChannelCode`            | `string`  | 是   | 采集通道的唯一标识符                                       |
| `BatchSize`              | `integer` | 否   | 批量写入数据库的数据点数量                                 |
| `AcquisitionInterval`    | `integer` | 是   | 数据采集的时间间隔（毫秒），0 表示最高频率采集（无延迟）   |
| `AcquisitionMode`        | `string`  | 是   | 采集模式（Always: 持续采集, Conditional: 条件触发采集）    |
| `EnableBatchRead`        | `boolean` | 否   | 是否启用批量读取功能                                       |
| `BatchReadRegister`      | `string`  | 否   | 批量读取的起始寄存器地址                                   |
| `BatchReadLength`        | `integer` | 否   | 批量读取的寄存器数量                                       |
| `Metrics`                | `array`   | 否   | 指标配置列表（条件采集模式下可为 null）                    |
| `ConditionalAcquisition` | `object`  | 否   | 条件采集配置（仅在 AcquisitionMode 为 Conditional 时需要） |

#### Metrics 数组属性

| 属性名称         | 类型      | 必填 | 说明                                        |
| ---------------- | --------- | ---- | ------------------------------------------- |
| `FieldName`      | `string`  | 是   | 时序数据库中的字段名称                      |
| `Register`       | `string`  | 是   | 指标对应的 PLC 寄存器地址                   |
| `Index`          | `integer` | 否   | 批量读取时在结果中的索引位置                |
| `DataType`       | `string`  | 是   | 数据类型（如 short, int, float 等）         |
| `EvalExpression` | `string`  | 否   | 数据转换表达式（使用 value 变量表示原始值） |

#### ConditionalAcquisition 对象属性

| 属性名称           | 类型     | 必填 | 说明                                                                      |
| ------------------ | -------- | ---- | ------------------------------------------------------------------------- |
| `Register`         | `string` | 是   | 条件触发监控的寄存器地址                                                  |
| `DataType`         | `string` | 是   | 条件触发寄存器的数据类型                                                  |
| `StartTriggerMode` | `string` | 是   | 开始采集的触发模式（RisingEdge: 数值增加触发, FallingEdge: 数值减少触发） |
| `EndTriggerMode`   | `string` | 是   | 结束采集的触发模式（RisingEdge: 数值增加触发, FallingEdge: 数值减少触发） |

### AcquisitionTrigger 触发模式说明

| 触发模式      | 说明                                          |
| ------------- | --------------------------------------------- |
| `RisingEdge`  | 当数值从较小值变为较大值时触发（prev < curr） |
| `FallingEdge` | 当数值从较大值变为较小值时触发（prev > curr） |

> 注意：此处的 RisingEdge 和 FallingEdge 与传统的边沿触发（0→1 或 1→0）不同，它们基于数值的增减变化来触发，而非严格的 0/1 跳变。

## Edge Agent 应用配置 (appsettings.json)

Edge Agent 的完整配置示例位于 `src/DataAcquisition.Edge.Agent/appsettings.json`：

```json
{
  "Urls": "http://localhost:8001",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "DatabasePath": "Data/logs.db"
  },
  "AllowedHosts": "*",
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token-here",
    "Bucket": "plc_data",
    "Org": "your-org"
  },
  "Parquet": {
    "Directory": "./Data/parquet"
  },
  "Edge": {
    "EnableCentralReporting": true,
    "CentralApiBaseUrl": "http://localhost:8000",
    "EdgeId": "EDGE-001",
    "HeartbeatIntervalSeconds": 10
  },
  "Acquisition": {
    "ChannelCollector": {
      "ConnectionCheckRetryDelayMs": 100,
      "TriggerWaitDelayMs": 100
    },
    "QueueService": {
      "FlushIntervalSeconds": 5,
      "RetryIntervalSeconds": 10,
      "MaxRetryCount": 3
    },
    "DeviceConfigService": {
      "ConfigChangeDetectionDelayMs": 500
    }
  }
}
```

### Edge Agent 配置项说明

| 配置项路径 | 类型 | 必填 | 默认值 | 说明 |
|-----------|------|------|--------|------|
| `Urls` | `string` | 否 | `http://localhost:8001` | Edge Agent 服务监听地址，支持多个地址（用 `;` 或 `,` 分隔） |
| `Logging:DatabasePath` | `string` | 否 | `Data/logs.db` | SQLite 日志数据库文件路径（相对路径相对于应用目录） |
| `InfluxDB:Url` | `string` | 是 | - | InfluxDB 服务器地址 |
| `InfluxDB:Token` | `string` | 是 | - | InfluxDB 认证令牌 |
| `InfluxDB:Bucket` | `string` | 是 | - | InfluxDB 存储桶名称 |
| `InfluxDB:Org` | `string` | 是 | - | InfluxDB 组织名称 |
| `Parquet:Directory` | `string` | 否 | `./Data/parquet` | Parquet WAL 文件存储目录（相对路径相对于应用目录） |
| `Edge:EnableCentralReporting` | `boolean` | 否 | `true` | 是否启用向 Central API 注册和心跳上报 |
| `Edge:CentralApiBaseUrl` | `string` | 否 | `http://localhost:8000` | Central API 服务地址 |
| `Edge:EdgeId` | `string` | 否 | 自动生成 | Edge 节点唯一标识符，为空时会自动生成并持久化到本地文件 |
| `Edge:HeartbeatIntervalSeconds` | `integer` | 否 | `10` | 向 Central API 发送心跳的间隔（秒） |
| `Acquisition:ChannelCollector:ConnectionCheckRetryDelayMs` | `integer` | 否 | `100` | PLC 连接检查重试延迟（毫秒） |
| `Acquisition:ChannelCollector:TriggerWaitDelayMs` | `integer` | 否 | `100` | 条件触发等待延迟（毫秒） |
| `Acquisition:QueueService:FlushIntervalSeconds` | `integer` | 否 | `5` | 队列批量刷新间隔（秒） |
| `Acquisition:QueueService:RetryIntervalSeconds` | `integer` | 否 | `10` | 重试间隔（秒） |
| `Acquisition:QueueService:MaxRetryCount` | `integer` | 否 | `3` | 最大重试次数 |
| `Acquisition:DeviceConfigService:ConfigChangeDetectionDelayMs` | `integer` | 否 | `500` | 设备配置文件变更检测延迟（毫秒） |

> **提示**：
> - 设备配置文件（PLC 配置）存放在 `Configs/` 目录下，格式为 `*.json`
> - 所有路径配置支持相对路径和绝对路径，相对路径相对于应用的工作目录
> - 配置支持通过环境变量覆盖，例如 `ASPNETCORE_URLS` 可覆盖 `Urls` 配置

## 📊 配置到数据库映射说明

系统将配置文件映射到 InfluxDB 时序数据库，以下是映射关系：

### 映射关系表

| 配置文件字段                        | InfluxDB 结构           | 说明                           | 示例值                       |
| ----------------------------------- | ----------------------- | ------------------------------ | ---------------------------- |
| `Channels[].Measurement`            | **Measurement**         | 时序数据库的测量名称（表名）   | `"sensor"`                   |
| `PlcCode`                           | **Tag**: `plc_code`     | PLC 设备编码标签               | `"M01C123"`                  |
| `Channels[].ChannelCode`            | **Tag**: `channel_code` | 通道编码标签                   | `"M01C01"`                   |
| `EventType`                         | **Tag**: `event_type`   | 事件类型标签（Start/End/Data） | `"Start"`, `"End"`, `"Data"` |
| `Channels[].Metrics[].FieldName`    | **Field**               | 数据字段名称                   | `"up_temp"`, `"down_temp"`   |
| `CycleId`                           | **Field**: `cycle_id`   | 采集周期唯一标识符（GUID）     | `"guid-xxx"`                 |
| 采集时间                            | **Timestamp**           | 数据点的时间戳（本地时间）     | `2025-01-15T10:30:00`       |

### 配置示例与 Line Protocol

**配置文件** (`M01C123.json`):

```json
{
  "IsEnabled": true,
  "PlcCode": "M01C123",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "M01C01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 10,
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Conditional",
      "Metrics": [
        {
          "MetricName": "up_temp",
          "FieldName": "up_temp",
          "Register": "D6002",
          "Index": 2,
          "DataType": "short"
        },
        {
          "MetricName": "down_temp",
          "FieldName": "down_temp",
          "Register": "D6004",
          "Index": 4,
          "DataType": "short",
          "EvalExpression": "value / 1000.0"
        }
      ],
      "ConditionalAcquisition": {
        "Register": "D6006",
        "DataType": "short",
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

**生成的 InfluxDB Line Protocol**:

**Start 事件**（条件采集开始）:

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Start up_temp=250i,down_temp=0.18,cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312200000000000
```

**Data 事件**（普通数据点）:

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Data up_temp=255i,down_temp=0.19 1705312210000000000
```

**End 事件**（条件采集结束）:

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=End cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312300000000000
```

### Line Protocol 格式说明

InfluxDB Line Protocol 格式：

```
measurement,tag1=value1,tag2=value2 field1=value1,field2=value2 timestamp
```

**字段类型说明**：

- **Measurement**: 来自配置的 `Measurement`，例如 `"sensor"`
- **Tags**（用于过滤和分组，索引字段）:
  - `plc_code`: PLC 设备编码
  - `channel_code`: 通道编码
  - `event_type`: 事件类型（`Start`/`End`/`Data`）
- **Fields**（实际数据值）:
  - 来自 `Metrics[].FieldName` 的所有字段（如 `up_temp`, `down_temp`）
  - `cycle_id`: 条件采集的周期 ID（GUID，用于关联 Start/End 事件）
  - 数值类型：整数使用 `i` 后缀（如 `250i`），浮点数直接写（如 `0.18`）
- **Timestamp**: 数据采集时间（本地时间，纳秒精度）

### 查询示例

**查询特定 PLC 的采集通道的指定时间（1h）范围的数据**:

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["plc_code"] == "M01C123")
  |> filter(fn: (r) => r["channel_code"] == "M01C01")
```

**查询条件采集的完整周期**:

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["cycle_id"] == "550e8400-e29b-41d4-a716-446655440000")
```

## 下一步

配置完成后，建议继续学习：

- 阅读 [API 使用示例](api-usage.md) 了解如何通过 API 查询数据和管理系统
