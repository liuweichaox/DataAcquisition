# Data Query Tutorial: InfluxDB and Metrics

This guide covers InfluxDB data model, Flux queries, and common examples.

---

## 1. Data Model

- Measurement: `sensor`, `production`
- Tag: `device_code`, `channel_code`
- Field: `temperature`, `pressure`

---

## 2. Basic Query

```flux
from(bucket: "iot")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["_field"] == "temperature")
```

---

## 3. Filter by Device

```flux
from(bucket: "iot")
  |> range(start: -1h)
  |> filter(fn: (r) => r["device_code"] == "PLC01")
```

---

## 4. Window Aggregation

```flux
from(bucket: "iot")
  |> range(start: -24h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> aggregateWindow(every: 1m, fn: mean)
```

---

## 5. Conditional Data (CycleId)

```flux
from(bucket: "iot")
  |> range(start: -7d)
  |> filter(fn: (r) => r["cycle_id"] == "CYCLE-2026-001")
```

---

## 6. Error Analysis

```flux
from(bucket: "iot")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "system")
  |> filter(fn: (r) => r["_field"] == "error_count")
```

---

## 7. Prometheus Metrics

Visit `/metrics` and visualize with Grafana.

---

Next: [Development Tutorial](tutorial-development.en.md)
