# 🔌 API 使用示例

本文档介绍 DataAcquisition 系统提供的 API 接口使用方法。

## 相关文档

- [快速开始指南](getting-started.md) - 从零开始使用系统
- [配置说明](configuration.md) - 详细的配置选项说明

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

**注意**：此 API 由 Edge Agent 提供，默认端口为 8001。

```bash
# 获取 PLC 连接状态
curl http://localhost:8001/api/DataAcquisition/GetPLCConnectionStatus
```

**响应示例：**

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

**响应示例：**

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

日志查询支持按级别、关键词过滤和分页。

**查询参数：**
- `level` (可选): 日志级别（如 "Error", "Warning", "Information"）
- `keyword` (可选): 关键词搜索（搜索日志消息内容）
- `page` (可选): 页码，默认值为 1
- `pageSize` (可选): 每页数量，默认值为 100

**请求示例：**

```bash
# 查询 Error 级别的日志（第1页，每页100条）
curl "http://localhost:8001/api/logs?level=Error&page=1&pageSize=100"

# 按关键词搜索日志
curl "http://localhost:8001/api/logs?keyword=InfluxDB&page=1&pageSize=50"
```

**响应示例：**

```json
{
  "data": [
    {
      "id": 1,
      "timestamp": "2025-01-15T10:30:00Z",
      "level": "Error",
      "message": "InfluxDB 写入失败: Connection timeout",
      "exception": "System.TimeoutException: ..."
    }
  ],
  "total": 150,
  "page": 1,
  "pageSize": 100,
  "totalPages": 2
}
```

### 获取日志级别

```bash
curl http://localhost:8001/api/logs/levels
```

**响应示例：**

```json
["Trace", "Debug", "Information", "Warning", "Error", "Critical"]
```

## 下一步

了解 API 使用后，建议继续学习：

- 阅读 [性能优化建议](performance.md) 了解如何优化系统性能

