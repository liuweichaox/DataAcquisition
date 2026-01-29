# Docker 部署指南：快速启动 InfluxDB

本指南说明如何使用 Docker Compose 快速部署和配置 InfluxDB 用于测试和开发。

[English](docker-influxdb.en.md) | 中文

## 快速开始

### 1. 启动 InfluxDB

```bash
# 进入项目根目录
cd DataAcquisition

# 启动 InfluxDB 容器
docker-compose up -d influxdb

# 查看容器状态
docker-compose ps
```

### 2. 初始化 InfluxDB

#### 方式一：通过 Web UI 初始化（推荐）

1. 打开浏览器访问：http://localhost:8086
2. 首次启动时会进入初始化界面
3. 填写以下信息：
   - **Username**: admin
   - **Password**: admin123
   - **Organization Name**: default
   - **Bucket Name**: iot (或自定义)
   - **Retention**: 30 days (或其他)

4. 生成 Token 后，记录下来（示例格式：`xxx...token...xxx`）

#### 方式二：通过命令行初始化

```bash
# 进入容器
docker-compose exec influxdb bash

# 使用 influx CLI 初始化
influx setup \
  --org default \
  --bucket iot \
  --retention 30d \
  --username admin \
  --password admin123 \
  --token my-super-secret-token \
  --force
```

### 3. 更新 Edge Agent 配置

编辑 `src/DataAcquisition.Edge.Agent/appsettings.json`，使用 InfluxDB 连接信息：

```json
{
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token-here",
    "Org": "default",
    "Bucket": "iot"
  }
}
```

### 4. 启动应用

```bash
# 启动 Edge Agent
dotnet run --project src/DataAcquisition.Edge.Agent

# 启动 Central API（另一个终端）
dotnet run --project src/DataAcquisition.Central.Api
```

---

## 容器管理

### 停止 InfluxDB

```bash
docker-compose down influxdb
```

### 停止并删除数据

```bash
docker-compose down -v
```

### 查看日志

```bash
docker-compose logs -f influxdb
```

### 备份数据

```bash
# 导出数据到文件
docker-compose exec influxdb influx backup /var/lib/influxdb2/backup

# 从本地复制出来
docker cp influxdb:/var/lib/influxdb2/backup ./backup
```

---

## 默认凭证

- **URL**: http://localhost:8086
- **Username**: admin
- **Password**: admin123
- **Organization**: default
- **Bucket**: iot

> **生产环境提示**: 请务必更改默认密码并使用环境变量管理敏感信息。

---

## 故障排查

### 容器无法启动

```bash
# 查看详细日志
docker-compose logs influxdb

# 检查端口是否被占用
lsof -i :8086
```

### Web UI 无法访问

```bash
# 确认容器正在运行
docker-compose ps

# 检查网络连接
docker-compose exec influxdb curl -v http://localhost:8086/api/v2/ready
```

### Token 创建失败

重新进入 Web UI 或使用 CLI 重新生成 Token。

---

## 扩展配置

若需自定义 InfluxDB 配置，编辑 `docker-compose.yml` 中的 `environment` 字段或挂载自定义配置文件。

---

## 下一步

- 返回 [README](../README.md)
- 返回 [文档索引](index.md)
- 阅读 [入门教程](tutorial-getting-started.md)

更多信息见：[InfluxDB 官方文档](https://docs.influxdata.com/influxdb/latest/)
