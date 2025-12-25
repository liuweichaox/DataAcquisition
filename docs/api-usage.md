# 🔌 API 使用示例

本文档介绍 DataAcquisition 系统提供的 API 接口使用方法。

## 指标数据查询

### Prometheus 格式指标

```bash
# 获取 Prometheus 格式指标
curl http://localhost:8000/metrics
```

### JSON 格式指标

```bash
# 获取 JSON 格式指标
curl http://localhost:8000/api/metrics-data

# 获取指标信息
curl http://localhost:8000/api/metrics-data/info
```

## PLC 连接状态查询

```bash
# 获取 PLC 连接状态
curl http://localhost:8000/api/DataAcquisition/GetPLCConnectionStatus
```

响应示例：

```json
{
  "plcCode": "M01C123",
  "isConnected": true,
  "lastConnectedTime": "2025-01-15T10:30:00Z"
}
```

## PLC 写入操作

### C# 客户端示例

```csharp
var request = new PLCWriteRequest
{
    PLCCode = "M01C123",
    Items = new List<PLCWriteItem>
    {
        new PLCWriteItem
        {
            Address = "D300",
            DataType = "short",
            Value = 100
        }
    }
};

var response = await httpClient.PostAsJsonAsync("/api/DataAcquisition/WriteRegister", request);
```

### HTTP 请求示例

```bash
curl -X POST http://localhost:8001/api/DataAcquisition/WriteRegister \
  -H "Content-Type: application/json" \
  -d '{
    "plcCode": "M01C123",
    "items": [
      {
        "address": "D300",
        "dataType": "short",
        "value": 100
      }
    ]
  }'
```

## 边缘节点管理

### 获取边缘节点列表

```bash
curl http://localhost:8000/api/edges
```

响应示例：

```json
[
  {
    "edgeId": "EDGE-001",
    "agentBaseUrl": "http://192.168.1.100:8001",
    "hostname": "WORKSTATION-01",
    "lastSeenUtc": "2025-01-15T10:30:00Z",
    "bufferBacklog": 0,
    "lastError": null
  }
]
```

## 日志查询

### 获取日志列表

```bash
# 查询 Edge Agent 日志
curl "http://localhost:8001/api/logs?level=Error&limit=100"

# 查询指定时间范围的日志
curl "http://localhost:8001/api/logs?startTime=2025-01-15T10:00:00Z&endTime=2025-01-15T11:00:00Z"
```

### 获取日志级别

```bash
curl http://localhost:8001/api/logs/levels
```

## 遥测数据上报

Edge Agent 向 Central API 上报遥测数据：

```bash
curl -X POST http://localhost:8000/api/telemetry/ingest \
  -H "Content-Type: application/json" \
  -d '{
    "edgeId": "EDGE-001",
    "batchId": "batch-123",
    "points": [
      {
        "measurement": "sensor",
        "tags": {
          "plc_code": "M01C123",
          "channel_code": "M01C01"
        },
        "fields": {
          "temperature": 25.5,
          "pressure": 1013.25
        },
        "timestamp": "2025-01-15T10:30:00Z"
      }
    ]
  }'
```
