# 配置到数据库映射说明

系统将配置文件映射到 InfluxDB 时序数据库，以下是映射关系：

## 映射关系表

| 配置文件字段                        | InfluxDB 结构           | 说明                           | 示例值                       |
| ----------------------------------- | ----------------------- | ------------------------------ | ---------------------------- |
| `Channels[].Measurement`            | **Measurement**         | 时序数据库的测量名称（表名）   | `"sensor"`                   |
| `PLCCode`                           | **Tag**: `plc_code`     | PLC 设备编码标签               | `"M01C123"`                  |
| `Channels[].ChannelCode`            | **Tag**: `channel_code` | 通道编码标签                   | `"M01C01"`                   |
| `EventType`                         | **Tag**: `event_type`   | 事件类型标签（Start/End/Data） | `"Start"`, `"End"`, `"Data"` |
| `Channels[].DataPoints[].FieldName` | **Field**               | 数据字段名称                   | `"up_temp"`, `"down_temp"`   |
| `CycleId`                           | **Field**: `cycle_id`   | 采集周期唯一标识符（GUID）     | `"guid-xxx"`                 |
| 采集时间                            | **Timestamp**           | 数据点的时间戳                 | `2025-01-15T10:30:00Z`       |

## 配置示例与 Line Protocol

### 配置文件示例 (`M01C123.json`)

```json
{
  "PLCCode": "M01C123",
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "M01C01",
      "DataPoints": [
        {
          "FieldName": "up_temp",
          "Register": "D6002",
          "Index": 2,
          "DataType": "short"
        },
        {
          "FieldName": "down_temp",
          "Register": "D6004",
          "Index": 4,
          "DataType": "short",
          "EvalExpression": "value / 1000.0"
        }
      ],
      "ConditionalAcquisition": {
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

### 生成的 InfluxDB Line Protocol

#### Start 事件（条件采集开始）

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Start up_temp=250i,down_temp=0.18,cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312200000000000
```

#### Data 事件（普通数据点）

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Data up_temp=255i,down_temp=0.19 1705312210000000000
```

#### End 事件（条件采集结束）

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=End cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312300000000000
```

## Line Protocol 格式说明

InfluxDB Line Protocol 格式：

```
measurement,tag1=value1,tag2=value2 field1=value1,field2=value2 timestamp
```

### 字段类型说明

- **Measurement**: 来自配置的 `Measurement`，例如 `"sensor"`
- **Tags**（用于过滤和分组，索引字段）:
  - `plc_code`: PLC 设备编码
  - `channel_code`: 通道编码
  - `event_type`: 事件类型（`Start`/`End`/`Data`）
- **Fields**（实际数据值）:
  - 来自 `DataPoints[].FieldName` 的所有字段（如 `up_temp`, `down_temp`）
  - `cycle_id`: 条件采集的周期 ID（GUID，用于关联 Start/End 事件）
  - 数值类型：整数使用 `i` 后缀（如 `250i`），浮点数直接写（如 `0.18`）
- **Timestamp**: 数据采集时间（纳秒精度）

## 查询示例

### 查询特定 PLC 的采集通道的指定时间（1h）范围的数据

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["plc_code"] == "M01C123")
  |> filter(fn: (r) => r["channel_code"] == "M01C01")
```

### 查询条件采集的完整周期

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["cycle_id"] == "550e8400-e29b-41d4-a716-446655440000")
```

## 相关文档

- [设备配置说明](./device-config.md)
- [Edge Agent 应用配置](./edge-agent-config.md)
