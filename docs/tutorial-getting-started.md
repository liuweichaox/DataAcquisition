# 入门教程：从零开始搭建 DataAcquisition

本教程面向第一次使用的用户，按步骤完成环境准备、模拟器启动、采集配置和验证。

---

## 1. 环境准备

### 必需软件

- .NET SDK 10.0+
- InfluxDB 2.x（本地安装或 Docker 部署，见下方说明）
- Node.js 18+（仅在使用 Central Web 时需要）

### 端口占用提示

- Central API 默认端口：`8000`
- Central Web 默认端口：`3000`
- InfluxDB 默认端口：`8086`

### InfluxDB 安装选项

#### 选项 A：本地安装（完整功能）

访问 [InfluxDB 官网](https://www.influxdata.com/downloads/) 下载并安装。

#### 选项 B：Docker 部署（推荐快速测试）

```bash
docker-compose up -d influxdb
```

详细说明见：[Docker InfluxDB 部署指南](docker-influxdb.md)

---

## 2. 获取代码

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition
```

---

## 3. 配置 InfluxDB

在 InfluxDB 中创建：

- Organization: `default`
- Bucket: `iot`
- Token: 任意生成

将以下配置写入 Edge Agent 的配置文件：

文件位置：`src/DataAcquisition.Edge.Agent/appsettings.json`

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

## 4. 启动 PLC 模拟器

模拟器用于替代真实 PLC，生成连续变化的数据。

```bash
cd src/DataAcquisition.Simulator
dotnet run
```

启动后会持续输出寄存器数据。

---

## 5. 配置设备文件

在 `src/DataAcquisition.Edge.Agent/Configs/` 目录创建配置文件，例如 `TEST_PLC.json`：

```json
{
  "IsEnabled": true,
  "PlcCode": "PLC01",
  "Host": "127.0.0.1",
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

---

## 6. 启动 Edge Agent

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
```

启动后，控制台会显示采集和写入日志。

---

## 7. 启动 Central API

```bash
dotnet run --project src/DataAcquisition.Central.Api
```

浏览器访问：

- HealthCheck: `http://localhost:8000/health`
- Metrics: `http://localhost:8000/metrics`

---

## 8. 启动 Central Web

```bash
cd src/DataAcquisition.Central.Web
npm install
npm run serve
```

浏览器访问：`http://localhost:3000`

---

## 9. 验证数据写入

在 InfluxDB 中执行 Flux 查询：

```flux
from(bucket: "iot")
  |> range(start: -10m)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> yield(name: "latest")
```

如果有数据返回，说明采集和存储成功。

---

## 10. 验证 WAL 机制

断开 InfluxDB 后观察：

- `src/DataAcquisition.Edge.Agent/Data/parquet/pending/` 出现 Parquet 文件
- InfluxDB 恢复后文件自动转移并清理

---

## 下一步

- 阅读 [配置教程](tutorial-configuration.md)
- 学习 [数据查询教程](tutorial-data-query.md)
- 了解 [部署教程](tutorial-deployment.md)
- 查看 [开发扩展教程](tutorial-development.md)
