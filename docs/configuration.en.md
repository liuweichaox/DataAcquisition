# ⚙️ Configuration Guide

This document provides detailed configuration instructions for the DataAcquisition system.

## Device Configuration Files

Device configuration files are located in the `src/DataAcquisition.Edge.Agent/Configs/` directory, with one JSON configuration file per PLC device.

> **Note**: For detailed configuration examples and property descriptions, please refer to the [Chinese version](configuration.md) (more complete). This English version provides a quick reference.

## Quick Configuration Reference

### Basic Device Configuration

```json
{
  "IsEnabled": true,
  "PLCCode": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "Channels": [
    {
      "Measurement": "temperature",
      "ChannelCode": "PLC01C01",
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Conditional",
      "DataPoints": [...]
    }
  ]
}
```

### Edge Agent Configuration (appsettings.json)

```json
{
  "Urls": "http://localhost:8001",
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-token-here",
    "Bucket": "plc_data",
    "Org": "your-org"
  },
  "Edge": {
    "EnableCentralReporting": true,
    "CentralApiBaseUrl": "http://localhost:8000",
    "EdgeId": "EDGE-001"
  }
}
```

For complete configuration documentation, see: [Chinese Configuration Guide](configuration.md)
