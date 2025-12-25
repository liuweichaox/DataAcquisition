# 设备配置说明

## 设备配置文件示例

```json
{
  "IsEnabled": true,
  "PLCCode": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "temperature",
      "ChannelCode": "PLC01C01",
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Conditional",
      "EnableBatchRead": true,
      "BatchReadRegister": "D200",
      "BatchReadLength": 20,
      "DataPoints": [
        {
          "FieldName": "temp_value",
          "Register": "D200",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value * 0.1"
        }
      ],
      "ConditionalAcquisition": {
        "Register": "D210",
        "DataType": "short",
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

## 设备配置属性详细说明

### 根级别属性

| 属性名称                   | 类型      | 必填 | 说明                                      |
| -------------------------- | --------- | ---- | ----------------------------------------- |
| `IsEnabled`                | `boolean` | 是   | 设备是否启用                              |
| `PLCCode`                  | `string`  | 是   | PLC 设备的唯一标识符                      |
| `Host`                     | `string`  | 是   | PLC 设备的 IP 地址                        |
| `Port`                     | `integer` | 是   | PLC 设备的通信端口                        |
| `Type`                     | `string`  | 是   | PLC 设备类型（如 Mitsubishi, Siemens 等） |
| `HeartbeatMonitorRegister` | `string`  | 否   | 用于监控 PLC 心跳的寄存器地址             |
| `HeartbeatPollingInterval` | `integer` | 否   | 心跳监控的轮询间隔（毫秒）                |
| `Channels`                 | `array`   | 是   | 数据采集通道配置列表                      |

### Channels 数组属性

| 属性名称                 | 类型      | 必填 | 说明                                                       |
| ------------------------ | --------- | ---- | ---------------------------------------------------------- |
| `Measurement`            | `string`  | 是   | 时序数据库中的测量名称（表名）                             |
| `ChannelCode`            | `string`  | 是   | 采集通道的唯一标识符                                       |
| `BatchSize`              | `integer` | 否   | 批量写入数据库的数据点数量                                 |
| `AcquisitionInterval`    | `integer` | 是   | 数据采集的时间间隔（毫秒）                                 |
| `AcquisitionMode`        | `string`  | 是   | 采集模式（Always: 持续采集, Conditional: 条件触发采集）    |
| `EnableBatchRead`        | `boolean` | 否   | 是否启用批量读取功能                                       |
| `BatchReadRegister`      | `string`  | 否   | 批量读取的起始寄存器地址                                   |
| `BatchReadLength`        | `integer` | 否   | 批量读取的寄存器数量                                       |
| `DataPoints`             | `array`   | 是   | 数据点配置列表                                             |
| `ConditionalAcquisition` | `object`  | 否   | 条件采集配置（仅在 AcquisitionMode 为 Conditional 时需要） |

### DataPoints 数组属性

| 属性名称         | 类型      | 必填 | 说明                                        |
| ---------------- | --------- | ---- | ------------------------------------------- |
| `FieldName`      | `string`  | 是   | 时序数据库中的字段名称                      |
| `Register`       | `string`  | 是   | 数据点对应的 PLC 寄存器地址                 |
| `Index`          | `integer` | 否   | 批量读取时在结果中的索引位置                |
| `DataType`       | `string`  | 是   | 数据类型（如 short, int, float 等）         |
| `EvalExpression` | `string`  | 否   | 数据转换表达式（使用 value 变量表示原始值） |

### ConditionalAcquisition 对象属性

| 属性名称           | 类型     | 必填 | 说明                                                                      |
| ------------------ | -------- | ---- | ------------------------------------------------------------------------- |
| `Register`         | `string` | 是   | 条件触发监控的寄存器地址                                                  |
| `DataType`         | `string` | 是   | 条件触发寄存器的数据类型                                                  |
| `StartTriggerMode` | `string` | 是   | 开始采集的触发模式（RisingEdge: 数值增加触发, FallingEdge: 数值减少触发） |
| `EndTriggerMode`   | `string` | 是   | 结束采集的触发模式（RisingEdge: 数值增加触发, FallingEdge: 数值减少触发） |

## AcquisitionTrigger 触发模式说明

| 触发模式      | 说明                                          |
| ------------- | --------------------------------------------- |
| `RisingEdge`  | 当数值从较小值变为较大值时触发（prev < curr） |
| `FallingEdge` | 当数值从较大值变为较小值时触发（prev > curr） |

> 注意：此处的 RisingEdge 和 FallingEdge 与传统的边沿触发（0→1 或 1→0）不同，它们基于数值的增减变化来触发，而非严格的 0/1 跳变。

## 配置文件位置

设备配置文件存放在 `src/DataAcquisition.Edge.Agent/Configs/` 目录下，格式为 `*.json`。系统会自动监控该目录下的配置文件变化，支持热更新，无需重启服务。

## 相关文档

- [Edge Agent 应用配置](./edge-agent-config.md)
- [配置到数据库映射](./database-mapping.md)
