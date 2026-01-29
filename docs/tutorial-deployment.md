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
npm install
npm run build
# 将 dist/ 部署到 nginx 或静态服务
```

---

## 4. Docker（可选）

项目未内置 Dockerfile，你可以参考以下模板自行创建：

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY ./publish/edge/ .
ENTRYPOINT ["dotnet", "DataAcquisition.Edge.Agent.dll"]
```

```yaml
version: "3.9"
services:
  edge-agent:
    build: .
    ports:
      - "9000:9000"
    volumes:
      - ./Data:/app/Data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
```

---

## 5. Nginx 反向代理

```nginx
server {
  listen 80;
  server_name your.domain.com;

  location /api/ {
    proxy_pass http://central-api:8000/;
  }

  location / {
    root /var/www/central-web;
    try_files $uri /index.html;
  }
}
```

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
