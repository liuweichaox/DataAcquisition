# Deployment Tutorial: From Dev to Production

This guide covers recommended production deployment and optional containerization.

---

## 1. Deployment Topology

### Single Workshop
- 1 Edge Agent
- Optional Central API/Web for monitoring

### Multi-Workshop
- Edge Agent per workshop
- Central API + Web in the core site

---

## 2. Runtime Requirements

- .NET 10.0 Runtime
- InfluxDB 2.x
- Node.js (Central Web only)

Use a service manager:
- Linux: systemd
- Windows: Windows Service

---

## 3. Process Deployment (Recommended)

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
# Serve dist/ using nginx or static host
```

---

## 4. Docker (Optional)

The repo doesn’t include Dockerfiles by default. You can create one as follows:

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

## 5. Nginx Reverse Proxy

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

## 6. Monitoring & Logs

- Prometheus: `/metrics`
- Health checks: `/health`
- Recommended: Grafana dashboards and alerting

---

## 7. Backup & Recovery

- Backup InfluxDB buckets regularly
- Backup `Data/` WAL files
- Use snapshots or object storage

---

## 8. Security Notes

- Keep Central API in internal network
- Use HTTPS
- Provide tokens via environment variables
- Follow least-privilege

---

Next: [Data Query Tutorial](tutorial-data-query.en.md)
