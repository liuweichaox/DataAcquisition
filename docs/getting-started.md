# 🚀 快速开始指南

本文档面向初学者，提供从零开始使用 DataAcquisition 系统的完整步骤。

## 前置要求

在开始之前，请确保已安装以下软件：

| 软件 | 版本要求 | 下载地址 | 说明 |
|------|---------|---------|------|
| .NET SDK | 8.0 或 10.0 | [.NET 官网](https://dotnet.microsoft.com/download) | 必须安装，用于运行系统 |
| Node.js | 18 或更高版本 | [Node.js 官网](https://nodejs.org/) | 用于运行前端界面（可选） |
| InfluxDB | 2.x | [InfluxDB 官网](https://www.influxdata.com/downloads/) | 时序数据库，生产环境推荐安装 |

## 第一步：获取项目

```bash
# 克隆项目到本地
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition

# 恢复项目依赖
dotnet restore
```

## 第二步：配置 InfluxDB（可选但推荐）

如果还没有安装 InfluxDB，可以：

1. **下载并安装 InfluxDB**：访问 [InfluxDB 官网](https://www.influxdata.com/downloads/) 下载对应平台的安装包
2. **启动 InfluxDB 服务**：按照官方文档启动服务（默认端口 8086）
3. **创建 Bucket 和 Token**：
   - 访问 InfluxDB UI（通常是 http://localhost:8086）
   - 创建 Organization（组织）
   - 创建 Bucket（存储桶，例如 `plc_data`）
   - 生成 Token（令牌）

## 第三步：配置 Edge Agent

### 3.1 配置应用设置

编辑 `src/DataAcquisition.Edge.Agent/appsettings.json`：

```json
{
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "你的-InfluxDB-Token",
    "Bucket": "plc_data",
    "Org": "你的-组织名称"
  }
}
```

**重要提示**：
- 如果还没有 InfluxDB，可以暂时使用示例 Token，但数据不会真正存储
- 生产环境建议使用环境变量管理敏感信息

### 3.2 创建设备配置文件

在 `src/DataAcquisition.Edge.Agent/Configs/` 目录下创建 PLC 设备配置文件。

**示例：创建一个名为 `MY_PLC.json` 的配置文件**

```json
{
  "IsEnabled": true,
  "PLCCode": "MY_PLC",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "CH01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 10,
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Always",
      "DataPoints": [
        {
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        },
        {
          "FieldName": "pressure",
          "Register": "D6001",
          "Index": 2,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        }
      ]
    }
  ]
}
```

**配置说明**：
- `PLCCode`: 为你的 PLC 设备起一个唯一的名字
- `Host`: PLC 设备的 IP 地址
- `Port`: PLC 设备的通信端口（通常是 502）
- `Type`: PLC 类型，必须是 `Mitsubishi`、`Inovance` 或 `BeckhoffAds` 之一
- `Channels`: 数据采集通道配置，可以配置多个通道

## 第四步：启动系统

### 4.1 启动 Central API（中心服务）

打开第一个终端窗口：

```bash
cd DataAcquisition
dotnet run --project src/DataAcquisition.Central.Api
```

看到以下输出表示启动成功：
```
Central API 服务已启动
服务地址: http://localhost:8000
```

### 4.2 启动 Edge Agent（边缘采集服务）

打开第二个终端窗口：

```bash
cd DataAcquisition
dotnet run --project src/DataAcquisition.Edge.Agent
```

看到以下输出表示启动成功：
```
Edge Agent 服务已启动
服务地址: http://localhost:8001
开始加载设备配置...
```

### 4.3 启动 Central Web（前端界面，可选）

打开第三个终端窗口：

```bash
cd DataAcquisition/src/DataAcquisition.Central.Web
npm install
npm run serve
```

看到以下输出表示启动成功：
```
App running at:
- Local:   http://localhost:3000/
```

## 第五步：验证系统运行

### 5.1 检查服务状态

1. **检查 Central API**：
   ```bash
   curl http://localhost:8000/health
   ```
   应该返回 `Healthy`

2. **检查 Edge Agent**：
   ```bash
   curl http://localhost:8001/api/DataAcquisition/GetPLCConnectionStatus
   ```
   应该返回 PLC 连接状态列表

3. **检查指标**：
   ```bash
   curl http://localhost:8000/api/metrics-data
   ```
   应该返回 JSON 格式的指标数据

### 5.2 访问 Web 界面

打开浏览器访问 http://localhost:3000，你应该能看到：
- 边缘节点列表
- 系统指标图表
- 日志查询界面

## 第六步：使用 PLC 模拟器进行测试

如果还没有真实的 PLC 设备，可以使用项目自带的模拟器进行测试：

### 6.1 启动模拟器

打开第四个终端窗口：

```bash
cd DataAcquisition/src/DataAcquisition.Simulator
dotnet run
```

模拟器会启动并监听 502 端口，模拟三菱 PLC 的行为。

### 6.2 配置测试设备

使用项目提供的 `TEST_PLC.json` 配置文件（已存在于 `src/DataAcquisition.Edge.Agent/Configs/` 目录），或创建新的配置文件：

```json
{
  "IsEnabled": true,
  "PLCCode": "TEST_PLC",
  "Host": "127.0.0.1",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 2000,
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "CH01",
      "EnableBatchRead": true,
      "BatchReadRegister": "D6000",
      "BatchReadLength": 14,
      "BatchSize": 10,
      "AcquisitionInterval": 0,
      "AcquisitionMode": "Always",
      "DataPoints": [
        {
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

### 6.3 观察数据采集

1. 启动 Edge Agent（如果还没启动）
2. 等待几秒钟让系统连接并开始采集
3. 访问 http://localhost:3000 查看采集到的数据
4. 检查 InfluxDB 中是否有数据写入

## 下一步

现在你已经成功启动了系统，接下来可以：

- 阅读 [配置说明](configuration.md) 了解详细的配置选项和使用场景
- 阅读 [API 使用文档](api-usage.md) 了解如何通过 API 查询数据和管理系统
- 阅读 [性能优化建议](performance.md) 了解如何优化系统性能
- 阅读 [常见问题](faq.md) 获取更多帮助

## 故障排查

### 问题 1：Edge Agent 无法连接 PLC

**检查步骤**：
1. 确认 PLC 设备 IP 和端口配置正确
2. 检查网络连通性：`ping <PLC_IP>`
3. 查看 Edge Agent 日志：访问 http://localhost:8001/api/logs
4. 检查 PLC 连接状态：访问 http://localhost:8001/api/DataAcquisition/GetPLCConnectionStatus

### 问题 2：数据没有写入 InfluxDB

**检查步骤**：
1. 确认 InfluxDB 服务正在运行
2. 检查 InfluxDB 配置（Url、Token、Bucket、Org）是否正确
3. 查看 `Data/parquet` 目录是否有 WAL 文件（如果有，说明写入失败）
4. 查看日志中的错误信息

### 问题 3：配置文件修改后没有生效

**解决方案**：
- 系统支持配置热更新，通常会在 500ms 内自动检测并重新加载
- 如果长时间没有生效，检查配置文件格式是否正确（JSON 语法）
- 查看日志确认配置加载情况

## 下一步

- 阅读 [配置说明](configuration.md) 了解详细的配置选项
- 阅读 [API 使用文档](api-usage.md) 了解如何通过 API 查询数据
- 阅读 [性能优化建议](performance.md) 了解如何优化系统性能
- 阅读 [常见问题](faq.md) 获取更多帮助

