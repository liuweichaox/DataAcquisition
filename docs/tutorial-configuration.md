# 配置教程：设备、通道与采集模式

本教程详解设备配置文件与应用配置，包含常见场景和最佳实践。

---

## 1. 配置文件位置

- 设备配置：`src/DataAcquisition.Edge.Agent/Configs/*.json`
- 应用配置：`src/DataAcquisition.Edge.Agent/appsettings.json`

---

## 2. 设备配置结构

```json
{
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
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
          "MetricName": "temperature",
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

### 字段说明

- `PlcCode`：设备唯一编号
- `Type`：PLC 类型（Mitsubishi/Inovance/BeckhoffAds）
- `HeartbeatMonitorRegister`：心跳寄存器地址
- `Channels`：采集通道列表（按业务或功能拆分）

---

## 3. 采集模式

### Always（持续采集）

适用于连续信号：温度、压力、电流等。

```json
{
  "AcquisitionMode": "Always",
  "AcquisitionInterval": 100
}
```

### Conditional（条件触发）

适用于事件驱动场景（生产周期、设备状态变化）。

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

> Conditional 模式会写入 Start/End 事件，并通过 CycleId 关联完整周期数据。

---

## 4. 批量读取配置

启用批量读取以减少网络往返：

```json
{
  "EnableBatchRead": true,
  "BatchReadRegister": "D6000",
  "BatchReadLength": 10
}
```

- `Index` 用于指定字段在批量返回中的位置

---

## 5. 数据转换表达式

可通过 `EvalExpression` 对原始值进行转换：

```json
{
  "FieldName": "temperature",
  "Register": "D6000",
  "DataType": "short",
  "EvalExpression": "value / 100.0"
}
```

---

## 6. 应用配置（appsettings.json）

核心配置示例：

```json
{
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token",
    "Org": "your-org",
    "Bucket": "plc_data"
  },
  "Parquet": {
    "Directory": "./Data/parquet"
  },
  "Edge": {
    "EnableCentralReporting": true,
    "CentralApiBaseUrl": "http://localhost:8000",
    "HeartbeatIntervalSeconds": 10
  }
}
```

---

## 7. 配置热更新

- 修改 `Configs/*.json` 会自动生效
- 默认 500ms 延迟，避免频繁触发

---

## 8. 最佳实践

- **统一命名**：PlcCode、ChannelCode 建议按业务规范
- **批量读取优先**：尽量将寄存器顺序连续化
- **合理 BatchSize**：过小浪费资源，过大增加延迟
- **数据转换**：在采集时完成单位换算

---

## 常见问题

- **采集不到数据**：检查 PLC 地址、端口、寄存器类型
- **Conditional 不触发**：确认触发寄存器变化与触发模式
- **InfluxDB 无数据**：检查 Token、Org、Bucket

---

下一步请阅读：[部署教程](tutorial-deployment.md)
