# Docker Deployment Guide: Quick Start InfluxDB

This guide explains how to quickly deploy and configure InfluxDB using Docker Compose for testing and development.

[中文](docker-influxdb.md) | English

## Quick Start

### 1. Start InfluxDB

```bash
# Navigate to project root
cd DataAcquisition

# Start InfluxDB container
docker-compose -f docker-compose.tsdb.yml up -d influxdb

# Check container status
docker-compose -f docker-compose.tsdb.yml ps
```

### 2. Initialize InfluxDB

#### Method 1: Web UI Initialization (Recommended)

1. Open browser and visit: http://localhost:8086
2. First launch will show initialization screen
3. Fill in the following information:
   - **Username**: admin
   - **Password**: admin123
   - **Organization Name**: default
   - **Bucket Name**: iot (or customize)
   - **Retention**: 30 days (or other)

4. After token generation, save it (example format: `xxx...token...xxx`)

#### Method 2: CLI Initialization

```bash
# Enter container
docker-compose -f docker-compose.tsdb.yml exec influxdb bash

# Use influx CLI to initialize
influx setup \
  --org default \
  --bucket iot \
  --retention 30d \
  --username admin \
  --password admin123 \
  --token my-super-secret-token \
  --force
```

### 3. Update Edge Agent Configuration

Edit `src/DataAcquisition.Edge.Agent/appsettings.json` with InfluxDB connection info:

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

### 4. Start Applications

```bash
# Start Edge Agent
dotnet run --project src/DataAcquisition.Edge.Agent

# Start Central API (in another terminal)
dotnet run --project src/DataAcquisition.Central.Api
```

---

## Container Management

### Stop InfluxDB

```bash
docker-compose -f docker-compose.tsdb.yml down influxdb
```

### Stop and Remove Data

```bash
docker-compose -f docker-compose.tsdb.yml down -v
```

### View Logs

```bash
docker-compose -f docker-compose.tsdb.yml logs -f influxdb
```

### Backup Data

```bash
# Export data to file
docker-compose -f docker-compose.tsdb.yml exec influxdb influx backup /var/lib/influxdb2/backup

# Copy backup from container
docker cp influxdb:/var/lib/influxdb2/backup ./backup
```

---

## Default Credentials

- **URL**: http://localhost:8086
- **Username**: admin
- **Password**: admin123
- **Organization**: default
- **Bucket**: iot

> **Production Tip**: Be sure to change default passwords and manage sensitive information using environment variables.

---

## Troubleshooting

### Container Failed to Start

```bash
# View detailed logs
docker-compose -f docker-compose.tsdb.yml logs influxdb

# Check if port is in use
lsof -i :8086
```

### Web UI Not Accessible

```bash
# Verify container is running
docker-compose -f docker-compose.tsdb.yml ps

# Check network connectivity
docker-compose -f docker-compose.tsdb.yml exec influxdb curl -v http://localhost:8086/api/v2/ready
```

### Token Creation Failed

Re-enter Web UI or use CLI to regenerate token.

---

## Extended Configuration

To customize InfluxDB configuration, edit the `environment` field in `docker-compose.tsdb.yml` or mount a custom configuration file.

---

## Next Steps

- Back to [README](../README.en.md)
- Back to [Documentation Index](index.en.md)
- Read [Getting Started Tutorial](tutorial-getting-started.en.md)

More information: [InfluxDB Official Documentation](https://docs.influxdata.com/influxdb/latest/)
