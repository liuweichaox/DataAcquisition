# 数据查询教程：InfluxDB 与指标分析

本教程介绍 InfluxDB 的数据模型、Flux 查询语法与常见查询示例。

---

## 1. 数据模型

- Measurement：如 `sensor`、`production`
- Tag：如 `device_code`、`channel_code`
- Field：如 `temperature`、`pressure`

---

## 2. 基础查询

查询最近 1 小时温度：

```flux
from(bucket: "iot")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["_field"] == "temperature")
```

---

## 3. 按设备过滤

```flux
from(bucket: "iot")
  |> range(start: -1h)
  |> filter(fn: (r) => r["device_code"] == "PLC01")
```

---

## 4. 时间窗口聚合

```flux
from(bucket: "iot")
  |> range(start: -24h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> aggregateWindow(every: 1m, fn: mean)
```

---

## 5. 条件采集数据（CycleId）

查询某次生产周期：

```flux
from(bucket: "iot")
  |> range(start: -7d)
  |> filter(fn: (r) => r["cycle_id"] == "CYCLE-2026-001")
```

---

## 6. 错误与异常分析

```flux
from(bucket: "iot")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "system")
  |> filter(fn: (r) => r["_field"] == "error_count")
```

---

## 7. Prometheus 指标

访问 `/metrics`，可结合 Grafana 绘图。

---

下一步阅读：[开发扩展教程](tutorial-development.md)
