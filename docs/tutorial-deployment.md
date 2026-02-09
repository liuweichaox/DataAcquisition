# 部署教程：从开发到生产

本教程提供生产环境部署建议与可选容器化方案。

---

## 1. 部署架构建议

### 单车间部署
- 1 个 Edge Agent
- 可选 Central API/Web（用于监控）

### 多车间部署
- 每车间部署 Edge Agent
- 中心部署 Central API + Central Web

---

## 2. 运行时准备

- .NET 10.0 Runtime
- InfluxDB 2.x
- Node.js（仅 Central Web）

建议使用服务管理器：
- Linux：systemd
- Windows：Windows Service

---

## 3. 进程级部署（推荐）

### Edge Agent

```bash
dotnet publish src/DataAcquisition.Edge.Agent -c Release -o ./publish/edge
./publish/edge/DataAcquisition.Edge.Agent
```

### Central API

```bash
dotnet publish src/DataAcquisition.Central.Api -c Release -o ./publish/central-api
./publish/central-api/DataAcquisition.Central.Api
```

### Central Web

```bash
cd src/DataAcquisition.Central.Web
pnpm install
pnpm run build
# 将 dist/ 部署到 nginx 或静态服务
```

---

## 4. Docker 容器化部署

项目提供两个独立的 Docker Compose 文件，可按需使用：

| 文件 | 内容 | 用途 |
|------|------|------|
| `docker-compose.tsdb.yml` | InfluxDB 2.7 | 时序数据库 |
| `docker-compose.app.yml` | Central API + Central Web | 中心应用服务 |

> **注意**：Edge Agent 需要直连 PLC 设备，始终通过进程直接运行（见上方第 3 节），不参与 Docker 部署。Edge Agent 启动时会自动检测本机真实 IP 并上报给 Central API，确保容器内的中心服务可以回调 Edge Agent 的诊断接口。

### 启动时序数据库

```bash
docker-compose -f docker-compose.tsdb.yml up -d
```

### 启动中心应用

```bash
docker-compose -f docker-compose.app.yml up -d --build
```

### 启动 Edge Agent（宿主机进程）

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
# 或使用 publish 后的二进制：
# ./publish/edge/DataAcquisition.Edge.Agent
```

### 访问地址

| 服务 | 地址 |
|------|------|
| Central Web | `http://localhost:3000` |
| Central API | `http://localhost:8000` |
| InfluxDB | `http://localhost:8086` |

### 全部启动

```bash
docker-compose -f docker-compose.tsdb.yml -f docker-compose.app.yml up -d
```

### 停止服务

```bash
docker-compose -f docker-compose.app.yml down
docker-compose -f docker-compose.tsdb.yml down
```

---

## 5. 架构说明

Docker 部署时的网络拓扑：

```
浏览器 → Central Web (nginx, :3000)
              ↓ /api/, /metrics, /health
         Central API (:8000, Docker 容器)
              ↓ 代理查询 Edge 诊断数据
         Edge Agent (:8001, 宿主机进程) → PLC 设备
```

Central Web 容器内置 nginx，负责：
- 提供前端静态文件
- 反向代理 `/api/`、`/metrics`、`/health` 到 Central API

如需自定义域名或 HTTPS，可在 Central Web 前再加一层外部 nginx/Caddy 反向代理。

---

## 6. 监控与日志

- Prometheus 指标：`/metrics`
- 健康检查：`/health`
- 建议接入 Grafana 和告警规则

---

## 7. 备份与恢复

- InfluxDB 定期备份 Bucket
- 备份 `Data/` 下的 Parquet WAL 文件
- 建议使用定期快照或对象存储

---

## 8. 安全建议

- Central API 建议部署在内网
- 使用 HTTPS
- Token/密码通过环境变量注入
- 最小权限原则

---

下一步阅读：[数据查询教程](tutorial-data-query.md)
