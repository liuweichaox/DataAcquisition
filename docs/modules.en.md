# 📊 Core Module Documentation

This document introduces the core module design and usage of the DataAcquisition system.

## PLC Client Implementations

| Protocol     | Implementation Class          | Description                         |
| ------------ | ----------------------------- | ----------------------------------- |
| Mitsubishi   | `MitsubishiPLCClientService`  | Mitsubishi PLC communication client |
| Inovance     | `InovancePLCClientService`    | Inovance PLC communication client   |
| Beckhoff ADS | `BeckhoffAdsPLCClientService` | Beckhoff ADS protocol client        |
| Siemens      | `SiemensPLCClientService`     | Siemens PLC communication client    |

## Key Modules

- **ChannelCollector**: Channel collector for data acquisition
- **InfluxDbDataStorageService**: Data storage service
- **MetricsCollector**: Metrics collection service

For detailed module documentation, see: [Chinese Module Guide](modules.md)
