# 🚀 Getting Started Guide

This document is designed for beginners and provides complete steps to get started with the DataAcquisition system from scratch.

## Prerequisites

Before you begin, ensure you have the following software installed:

| Software | Version Requirement | Download | Notes |
|----------|---------------------|----------|-------|
| .NET SDK | 8.0 or 10.0 | [.NET Official](https://dotnet.microsoft.com/download) | Required to run the system |
| Node.js | 18 or higher | [Node.js Official](https://nodejs.org/) | For running frontend interface (optional) |
| InfluxDB | 2.x | [InfluxDB Official](https://www.influxdata.com/downloads/) | Time-series database, recommended for production |

## Step 1: Get the Project

```bash
# Clone the project to local
git clone https://github.com/liuweichaox/DataAcquisition.git
cd DataAcquisition

# Restore project dependencies
dotnet restore
```

## Step 2: Configure InfluxDB (Optional but Recommended)

If you haven't installed InfluxDB yet:

1. **Download and Install InfluxDB**: Visit [InfluxDB Official](https://www.influxdata.com/downloads/) to download the installer for your platform
2. **Start InfluxDB Service**: Follow the official documentation to start the service (default port 8086)
3. **Create Bucket and Token**:
   - Access InfluxDB UI (usually http://localhost:8086)
   - Create Organization
   - Create Bucket (e.g., `plc_data`)
   - Generate Token

## Step 3: Configure Edge Agent

### 3.1 Configure Application Settings

Edit `src/DataAcquisition.Edge.Agent/appsettings.json`:

```json
{
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-influxdb-token",
    "Bucket": "plc_data",
    "Org": "your-organization-name"
  }
}
```

**Important Notes**:
- If you don't have InfluxDB yet, you can temporarily use a sample token, but data won't be actually stored
- Production environments should use environment variables to manage sensitive information

### 3.2 Create Device Configuration File

Create a PLC device configuration file in the `src/DataAcquisition.Edge.Agent/Configs/` directory.

**Example: Create a configuration file named `MY_PLC.json`**

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
      "Metrics": [
        {
          "MetricName": "temperature",
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value / 100.0"
        },
        {
          "MetricName": "pressure",
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

**Configuration Notes**:
- `PLCCode`: Give your PLC device a unique name
- `Host`: IP address of the PLC device
- `Port`: Communication port of the PLC device (usually 502)
- `Type`: PLC type, must be one of `Mitsubishi`, `Inovance`, or `BeckhoffAds`
- `Channels`: Data acquisition channel configuration, you can configure multiple channels

## Step 4: Start the System

### 4.1 Start Central API (Central Service)

Open the first terminal window:

```bash
cd DataAcquisition
dotnet run --project src/DataAcquisition.Central.Api
```

You should see output indicating successful startup:
```
Central API service started
Service address: http://localhost:8000
```

### 4.2 Start Edge Agent (Edge Acquisition Service)

Open the second terminal window:

```bash
cd DataAcquisition
dotnet run --project src/DataAcquisition.Edge.Agent
```

You should see output indicating successful startup:
```
Edge Agent service started
Service address: http://localhost:8001
Starting to load device configurations...
```

### 4.3 Start Central Web (Frontend Interface, Optional)

Open the third terminal window:

```bash
cd DataAcquisition/src/DataAcquisition.Central.Web
npm install
npm run serve
```

You should see output indicating successful startup:
```
App running at:
- Local:   http://localhost:3000/
```

## Step 5: Verify System Operation

### 5.1 Check Service Status

1. **Check Central API**:
   ```bash
   curl http://localhost:8000/health
   ```
   Should return `Healthy`

2. **Check Edge Agent**:
   ```bash
   curl http://localhost:8001/api/DataAcquisition/plc-connections
   ```
   Should return PLC connection status list

3. **Check Metrics**:
   ```bash
   curl http://localhost:8000/api/metrics-data
   ```
   Should return JSON format metrics data

### 5.2 Access Web Interface

Open your browser and visit http://localhost:3000, you should see:
- Edge node list
- System metrics charts
- Log query interface

## Step 6: Test with PLC Simulator

If you don't have a real PLC device yet, you can use the built-in simulator for testing:

### 6.1 Start the Simulator

Open the fourth terminal window:

```bash
cd DataAcquisition/src/DataAcquisition.Simulator
dotnet run
```

The simulator will start and listen on port 502, simulating Mitsubishi PLC behavior.

### 6.2 Configure Test Device

Use the `TEST_PLC.json` configuration file provided in the project (already exists in `src/DataAcquisition.Edge.Agent/Configs/` directory), or create a new configuration file:

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

### 6.3 Observe Data Acquisition

1. Start Edge Agent (if not already started)
2. Wait a few seconds for the system to connect and start acquisition
3. Visit http://localhost:3000 to view acquired data
4. Check if data has been written to InfluxDB

## Next Steps

After completing the getting started guide, we recommend continuing in the following order:

1. Read [Configuration Guide](configuration.en.md) to learn about detailed configuration options and usage scenarios

## Troubleshooting

### Issue 1: Edge Agent Cannot Connect to PLC

**Check Steps**:
1. Confirm PLC device IP and port configuration are correct
2. Check network connectivity: `ping <PLC_IP>`
3. View Edge Agent logs: Visit http://localhost:8001/api/logs
4. Check PLC connection status: Visit http://localhost:8001/api/DataAcquisition/plc-connections

### Issue 2: Data Not Written to InfluxDB

**Check Steps**:
1. Confirm InfluxDB service is running
2. Check InfluxDB configuration (Url, Token, Bucket, Org) is correct
3. Check if there are WAL files in `Data/parquet` directory (if yes, write failed)
4. Check error messages in logs

### Issue 3: Configuration Changes Not Taking Effect

**Solution**:
- The system supports configuration hot reload, usually detects and reloads within 500ms
- If it doesn't take effect for a long time, check if the configuration file format is correct (JSON syntax)
- Check logs to confirm configuration loading status

## Next Steps

- Read [Configuration Guide](configuration.en.md) to learn about detailed configuration options
- Read [API Usage Documentation](api-usage.en.md) to learn how to query data via API
- Read [Performance Optimization Recommendations](performance.en.md) to learn how to optimize system performance
- Read [FAQ](faq.en.md) for more help
