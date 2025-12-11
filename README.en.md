# 🛰️ PLC Data Acquisition System

.NET 8 | Windows / Linux / macOS
中文: [README.md](README.md)

---

## 📙 Overview

- Multi-PLC acquisition (conditional & unconditional), batch read support.
- Unified BatchSize end-to-end: when BatchSize is met, write Parquet (WAL) immediately, then write Influx immediately; delete WAL on success, keep on failure; RetryWorker retries every 5s.
- Hot config reload (JSON + FileSystemWatcher); local time only; Prometheus metrics + Vue3/Element Plus UI (multi-select, persisted selection).

## 🏗️ Architecture & Flow

```
PLC → HeartbeatMonitor → ChannelCollector → DataProcessingService
   → LocalQueueService (batching)
   → Parquet WAL (BatchSize met)
   → Immediate Influx write (delete on success, keep on failure)
   → ParquetRetryWorker (5s retry)
```

Components: Acquisition (ChannelCollector/HeartbeatMonitor/DataAcquisitionService), Queue (LocalQueueService), Storage (Parquet/Influx), Background (ParquetRetryWorker), Config (DeviceConfigService), Metrics (/metrics/raw, /metrics UI).

## 🚀 Quick Start

```bash
dotnet restore
dotnet build
dotnet run --project DataAcquisition.Gateway
# Visit: http://localhost:8000/metrics (UI)   http://localhost:8000/metrics/raw (Prometheus)
```

## ⚙️ Acquisition Config (YAML spec; actual JSON under `DataAcquisition.Gateway/Configs/*.json`)

```yaml
IsEnabled: true # enable device
Code: "PLC01" # device code
Host: "192.168.1.100" # PLC IP
Port: 502 # port
Type: ModbusTcp # plc type
HeartbeatMonitorRegister: "D100"
HeartbeatPollingInterval: 5000 # ms

Channels:
  - Measurement: "temperature" # measurement name
    BatchSize: 10 # batch size (pipeline unified)
    AcquisitionInterval: 100 # ms, 0 = as fast as possible
    AcquisitionMode: Conditional # Conditional or Always
    EnableBatchRead: true
    BatchReadRegister: "D200"
    BatchReadLength: 20
    DataPoints:
      - FieldName: "temp_value"
        Register: "D200"
        Index: 0
        DataType: float
        EvalExpression: "value * 0.1"
    ConditionalAcquisition:
      Register: "D210"
      DataType: short
      Start:
        TriggerMode: RisingEdge # Always/RisingEdge/FallingEdge/ValueIncrease/ValueDecrease
        TimestampField: "start_time"
      End:
        TriggerMode: FallingEdge
        TimestampField: "end_time"
```

Notes:

- `BatchSize`: shared by acquisition → queue → WAL → Influx; when full, write WAL + Influx, delete on success.
- `AcquisitionInterval`: 0 = as fast as possible; watch PLC load for high frequency.
- Batch read: enable `EnableBatchRead` for contiguous registers; set start/length and `Index` correctly.
- Conditional: multiple trigger modes; optional `TimestampField` for start/end time.
- WAL tuning: `Parquet:Directory`, `Parquet:MaxFileSize`, `Parquet:MaxFileAge`.

## 🔧 API / UI

- Prometheus: `/metrics/raw`
- Metrics UI: `/metrics` (multi-select with persistence)
- SignalR Hub: `/dataHub` (real-time push; see code)
- Example: `GET /api/metrics-data` (metrics JSON)

## 📊 Metrics (examples)

- `data_acquisition_collection_latency_ms`, `data_acquisition_collection_rate`
- `data_acquisition_queue_depth`
- `data_acquisition_write_latency_ms`
- `data_acquisition_errors_total`
- `data_acquisition_connection_status_changes_total`, `data_acquisition_connection_duration_seconds`
- HTTP: `http_request_duration_seconds`, etc.

## 📌 Tuning

- BatchSize: 1-10 low latency; 10-50 general; 50+ throughput-first.
- AcquisitionInterval: 1-100ms high frequency; 100-1000ms typical; >1000ms slow-changing.
- Storage/Retry: adjust Parquet directory/size/age; RetryWorker interval (default 5s) if needed.

## 🛠️ Extensibility

- PLC comms: implement `IPlcClientService` / `IPlcClientFactory`
- Processing: implement `IDataProcessingService`
- Config: implement `IDeviceConfigService`
- Storage: replace `IDataStorageService` (honor queue write contract)

## 🚢 Deploy

```bash
dotnet publish DataAcquisition.Gateway -c Release -r win-x64 --self-contained true
dotnet publish DataAcquisition.Gateway -c Release -r linux-x64 --self-contained true
```

## 📜 License

MIT, see LICENSE.
