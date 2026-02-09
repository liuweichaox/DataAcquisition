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
pnpm install
pnpm run build
# Serve dist/ using nginx or static host
```

---

## 4. Docker Deployment

The project provides two separate Docker Compose files that can be used independently:

| File | Contents | Purpose |
|------|----------|---------|
| `docker-compose.tsdb.yml` | InfluxDB 2.7 | Time-series database |
| `docker-compose.app.yml` | Central API + Central Web | Central application |

> **Note**: Edge Agent needs direct access to PLC devices and always runs as a host process (see Section 3 above) — it is not containerized. Edge Agent auto-detects the local machine's real IP at startup and reports it to Central API, ensuring the containerized central services can callback to Edge Agent's diagnostic endpoints.

### Start the time-series database

```bash
docker-compose -f docker-compose.tsdb.yml up -d
```

### Start the central application

```bash
docker-compose -f docker-compose.app.yml up -d --build
```

### Start Edge Agent (host process)

```bash
dotnet run --project src/DataAcquisition.Edge.Agent
# Or use published binary:
# ./publish/edge/DataAcquisition.Edge.Agent
```

### Access URLs

| Service | URL |
|---------|-----|
| Central Web | `http://localhost:3000` |
| Central API | `http://localhost:8000` |
| InfluxDB | `http://localhost:8086` |

### Start everything

```bash
docker-compose -f docker-compose.tsdb.yml -f docker-compose.app.yml up -d
```

### Stop services

```bash
docker-compose -f docker-compose.app.yml down
docker-compose -f docker-compose.tsdb.yml down
```

---

## 5. Architecture Notes

Network topology in Docker deployment:

```
Browser → Central Web (nginx, :3000)
              ↓ /api/, /metrics, /health
         Central API (:8000, Docker container)
              ↓ proxy queries Edge diagnostic data
         Edge Agent (:8001, host process) → PLC devices
```

The Central Web container has a built-in nginx that handles:
- Serving frontend static files
- Reverse proxying `/api/`, `/metrics`, `/health` to Central API

If you need custom domains or HTTPS, add an external nginx/Caddy reverse proxy in front of Central Web.

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
