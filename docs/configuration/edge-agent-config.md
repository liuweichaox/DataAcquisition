# Edge Agent 应用配置说明

## 配置文件位置

Edge Agent 的完整配置示例位于 `src/DataAcquisition.Edge.Agent/appsettings.json`。

## 配置示例

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

## 配置项说明

### 基础配置

| 配置项路径 | 类型 | 必填 | 默认值 | 说明 |
|-----------|------|------|--------|------|
| `Urls` | `string` | 否 | `http://localhost:8001` | Edge Agent 服务监听地址，支持多个地址（用 `;` 或 `,` 分隔） |

### 日志配置

| 配置项路径 | 类型 | 必填 | 默认值 | 说明 |
|-----------|------|------|--------|------|
| `Logging:DatabasePath` | `string` | 否 | `Data/logs.db` | SQLite 日志数据库文件路径（相对路径相对于应用目录） |

### InfluxDB 配置

| 配置项路径 | 类型 | 必填 | 默认值 | 说明 |
|-----------|------|------|--------|------|
| `InfluxDB:Url` | `string` | 是 | - | InfluxDB 服务器地址 |
| `InfluxDB:Token` | `string` | 是 | - | InfluxDB 认证令牌 |
| `InfluxDB:Bucket` | `string` | 是 | - | InfluxDB 存储桶名称 |
| `InfluxDB:Org` | `string` | 是 | - | InfluxDB 组织名称 |

### Parquet 配置

| 配置项路径 | 类型 | 必填 | 默认值 | 说明 |
|-----------|------|------|--------|------|
| `Parquet:Directory` | `string` | 否 | `./Data/parquet` | Parquet WAL 文件存储目录（相对路径相对于应用目录） |

### Edge 节点配置

| 配置项路径 | 类型 | 必填 | 默认值 | 说明 |
|-----------|------|------|--------|------|
| `Edge:EnableCentralReporting` | `boolean` | 否 | `true` | 是否启用向 Central API 注册和心跳上报 |
| `Edge:CentralApiBaseUrl` | `string` | 否 | `http://localhost:8000` | Central API 服务地址 |
| `Edge:EdgeId` | `string` | 否 | 自动生成 | Edge 节点唯一标识符，为空时会自动生成并持久化到本地文件 |
| `Edge:HeartbeatIntervalSeconds` | `integer` | 否 | `10` | 向 Central API 发送心跳的间隔（秒） |

### 采集服务配置

| 配置项路径 | 类型 | 必填 | 默认值 | 说明 |
|-----------|------|------|--------|------|
| `Acquisition:ChannelCollector:ConnectionCheckRetryDelayMs` | `integer` | 否 | `100` | PLC 连接检查重试延迟（毫秒） |
| `Acquisition:ChannelCollector:TriggerWaitDelayMs` | `integer` | 否 | `100` | 条件触发等待延迟（毫秒） |
| `Acquisition:QueueService:FlushIntervalSeconds` | `integer` | 否 | `5` | 队列批量刷新间隔（秒） |
| `Acquisition:QueueService:RetryIntervalSeconds` | `integer` | 否 | `10` | 重试间隔（秒） |
| `Acquisition:QueueService:MaxRetryCount` | `integer` | 否 | `3` | 最大重试次数 |
| `Acquisition:DeviceConfigService:ConfigChangeDetectionDelayMs` | `integer` | 否 | `500` | 设备配置文件变更检测延迟（毫秒） |

## 配置提示

- 设备配置文件（PLC 配置）存放在 `Configs/` 目录下，格式为 `*.json`
- 所有路径配置支持相对路径和绝对路径，相对路径相对于应用的工作目录
- 配置支持通过环境变量覆盖，例如 `ASPNETCORE_URLS` 可覆盖 `Urls` 配置

## 相关文档

- [设备配置说明](./device-config.md)
- [配置到数据库映射](./database-mapping.md)
