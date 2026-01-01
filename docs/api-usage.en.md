# 🔌 API Usage Examples

This document introduces how to use the APIs provided by the DataAcquisition system.

## Related Documents

- [Getting Started Guide](getting-started.en.md) - Get started from scratch
- [Configuration Guide](configuration.en.md) - Detailed configuration options

## Metrics Data Query

### Prometheus Format Metrics

```bash
# Get Prometheus format metrics
curl http://localhost:8000/metrics
```

### JSON Format Metrics

```bash
# Get JSON format metrics
curl http://localhost:8000/api/metrics-data

# Get metrics information
curl http://localhost:8000/api/metrics-data/info
```

## PLC Connection Status Query

**Note**: This API is provided by Edge Agent, default port is 8001.

```bash
# Get all PLC connection statuses
curl http://localhost:8001/api/DataAcquisition/plc-connections
```

**Response Example:**

```json
[
  {
    "plcCode": "M01C123",
    "isConnected": true,
    "lastConnectedTime": "2025-01-15T10:30:00",
    "connectionDurationSeconds": 3600.5,
    "lastError": null
  },
  {
    "plcCode": "PLC02",
    "isConnected": false,
    "lastConnectedTime": "2025-01-15T09:00:00",
    "connectionDurationSeconds": null,
    "lastError": "Connection timeout"
  }
]
```

## PLC Write Operation

### C# Client Example

```csharp
var request = new PlcWriteRequest
{
    PlcCode = "M01C123",
    Items = new List<PlcWriteItem>
    {
        new PlcWriteItem
        {
            Address = "D300",
            DataType = "short",
            Value = 100
        }
    }
};

var response = await httpClient.PostAsJsonAsync("/api/DataAcquisition/WriteRegister", request);
```

### HTTP Request Example

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

## Edge Node Management

### Get Edge Node List

```bash
curl http://localhost:8000/api/edges
```

**Response Example:**

```json
[
  {
    "edgeId": "EDGE-001",
    "agentBaseUrl": "http://192.168.1.100:8001",
    "hostname": "WORKSTATION-01",
    "lastSeen": "2025-01-15T10:30:00",
    "bufferBacklog": 0,
    "lastError": null
  }
]
```

## Log Query

### Get Log List

Log query supports filtering by level, keyword search, and pagination.

**Query Parameters:**
- `level` (optional): Log level (e.g., "Error", "Warning", "Information")
- `keyword` (optional): Keyword search (searches log message content)
- `page` (optional): Page number, default value is 1
- `pageSize` (optional): Items per page, default value is 100

**Request Examples:**

```bash
# Query Error level logs (page 1, 100 items per page)
curl "http://localhost:8001/api/logs?level=Error&page=1&pageSize=100"

# Search logs by keyword
curl "http://localhost:8001/api/logs?keyword=InfluxDB&page=1&pageSize=50"
```

**Response Example:**

```json
{
  "data": [
    {
      "id": 1,
      "timestamp": "2025-01-15T10:30:00",
      "level": "Error",
      "message": "InfluxDB write failed: Connection timeout",
      "exception": "System.TimeoutException: ..."
    }
  ],
  "total": 150,
  "page": 1,
  "pageSize": 100,
  "totalPages": 2
}
```

### Get Log Levels

```bash
curl http://localhost:8001/api/logs/levels
```

**Response Example:**

```json
["Trace", "Debug", "Information", "Warning", "Error", "Critical"]
```

## Next Steps

After learning about API usage, we recommend continuing to learn:

- Read [Performance Optimization Recommendations](performance.en.md) to learn how to optimize system performance

