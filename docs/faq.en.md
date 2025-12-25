# ❓ Frequently Asked Questions (FAQ)

This document collects common questions and answers about the DataAcquisition system.

## Related Documents

- [Getting Started Guide](getting-started.en.md) - Get started from scratch
- [Configuration Guide](configuration.en.md) - Detailed configuration options
- [API Usage Examples](api-usage.en.md) - API interface usage methods
- [Performance Optimization Recommendations](performance.en.md) - Optimize system performance
- [Core Module Documentation](modules.en.md) - Understand system core modules
- [Data Processing Flow](data-flow.en.md) - Understand data flow process
- [Design Philosophy](design.en.md) - Understand system design philosophy

## Q: What if data is lost?

**A**: The system uses a WAL-first architecture. All data is first written to Parquet files, then to InfluxDB. WAL files are only deleted when both writes succeed, ensuring zero data loss.

If data loss is detected, you can:

1. Check if there are unprocessed WAL files in the `Data/parquet` directory
2. Check logs to confirm the reason for write failures
3. The system will automatically retry failed write operations

## Q: How to add a new PLC protocol?

**A**: Requires modifying source code, implementing the `IPLCClientService` interface and registering in `PLCClientFactory`.

**Steps**:

1. Create a new PLC client class implementing the `IPLCClientService` interface
2. Add protocol type mapping in `PLCClientFactory`
3. Add new protocol type to `PlcType` enum
4. Use the new protocol type in device configuration

**Note**: This requires modifying source code and recompiling, recommended for users with development experience.

## Q: Do I need to restart after configuration changes?

**A**: No. The system uses FileSystemWatcher to monitor configuration file changes, supporting hot updates.

After configuration file changes, the system will automatically:

1. Detect configuration file changes
2. Validate configuration format
3. Reload configuration
4. Apply new configuration (no service restart required)

## Q: Where to view monitoring metrics?

**A**: Visit http://localhost:8000/metrics to view the visualization interface or get Prometheus format metrics, or http://localhost:8000/api/metrics-data to get JSON format metrics data (recommended).

### Prometheus Format

```bash
curl http://localhost:8000/metrics
```

### JSON Format

```bash
curl http://localhost:8000/api/metrics-data
```

### Web Interface

Visit the Central Web interface (http://localhost:3000) to view visualized monitoring metrics.

## Q: How to extend storage backend?

**A**: Requires modifying source code, implementing the `IDataStorageService` interface and registering in `Program.cs`.

**Steps**:

1. Create a new storage service class implementing the `IDataStorageService` interface
2. Register the new storage service in `Program.cs`
3. The system will use multiple storage backends simultaneously

**Note**: This requires modifying source code and recompiling, recommended for users with development experience.

## Q: How to adjust acquisition frequency?

**A**: Modify the `AcquisitionInterval` parameter in the device configuration file (unit: milliseconds).

```json
{
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "CH01",
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Always",
      "BatchSize": 10,
      "DataPoints": [
        {
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short"
        }
      ]
    }
  ]
}
```

## Q: How to configure conditional acquisition?

**A**: Set `AcquisitionMode` to `Conditional` in the channel configuration and configure the `ConditionalAcquisition` object.

```json
{
  "Channels": [
    {
      "Measurement": "production",
      "ChannelCode": "CH01",
      "EnableBatchRead": false,
      "BatchReadRegister": null,
      "BatchReadLength": 0,
      "BatchSize": 1,
      "AcquisitionInterval": 0,
      "AcquisitionMode": "Conditional",
      "DataPoints": null,
      "ConditionalAcquisition": {
        "Register": "D210",
        "DataType": "short",
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

## Q: How to troubleshoot connection issues?

**A**: Follow these steps:

1. **Check PLC Connection Status**: 
   ```bash
   curl http://localhost:8001/api/DataAcquisition/GetPLCConnectionStatus
   ```
   View the returned connection status information

2. **Check Network Connectivity**: 
   ```bash
   ping <PLC_IP_ADDRESS>
   telnet <PLC_IP_ADDRESS> <PORT>
   ```
   Confirm that Edge Agent can access the PLC's IP and port

3. **Check Configuration Correctness**: 
   - Confirm `Host` and `Port` parameters in device configuration file are correct
   - Confirm `Type` parameter matches the actual PLC type (Mitsubishi, Inovance, BeckhoffAds)
   - Confirm `PLCCode` is not empty and unique

4. **View Log Information**: 
   ```bash
   curl "http://localhost:8001/api/logs?level=Error&page=1&pageSize=10"
   ```
   View error logs to identify specific issues

## Q: What to do if there are too many WAL files?

**A**: Too many WAL files usually indicates InfluxDB write failures. Solutions:

1. Check InfluxDB connection and configuration
2. Check logs to confirm the reason for write failures
3. After fixing the issue, the system will automatically process accumulated WAL files
4. If manual cleanup is needed, first confirm that data has been written to InfluxDB

## Q: How to deploy to production environment?

**A**: Recommended steps:

1. **Configure Production Parameters**: Modify configuration in `appsettings.json`
2. **Set Environment Variables**: Use environment variables to manage sensitive information (such as Token)
3. **Configure Log Level**: Production environment is recommended to use Warning level
4. **Enable Monitoring**: Configure Prometheus monitoring and alerts
5. **Backup Strategy**: Configure backup strategies for WAL files and databases

## Q: Which PLC protocols are supported?

**A**: The following PLC protocols are currently supported:

- Mitsubishi
- Inovance
- BeckhoffAds

Other protocols can be extended by implementing the `IPLCClientService` interface and registering in `PLCClientFactory`.

## Q: What to do if configuration file format is incorrect?

**A**: Configuration files must be valid JSON format. Common errors:

1. **JSON Format Error**: Check for missing commas, unclosed quotes, etc.
2. **Missing Required Fields**: Ensure required fields like `PLCCode`, `Host`, `Port`, `Type`, `Channels` exist
3. **Field Type Error**: Ensure `Port` is a number, `IsEnabled` is a boolean, etc.

**Verification Methods**:
- Use JSON validation tools (such as online JSON validators) to check format
- Check configuration loading error messages in logs
- The system validates configuration at startup, errors are recorded in logs

## Q: What to do if acquisition tasks don't start?

**A**: Check the following:

1. **Is Device Enabled**: Confirm `IsEnabled` is `true` in configuration file
2. **Are There Acquisition Channels**: Confirm `Channels` array is not empty
3. **View Startup Logs**: Check logs for "启动采集任务失败" (Failed to start acquisition task) error messages
4. **Check Configuration Path**: Confirm configuration file is in `Configs/` directory and filename ends with `.json`

**Common Errors**:
- "设备编码为空" (Device code is empty): Check if `PLCCode` is configured
- "没有配置采集通道" (No acquisition channels configured): Check if `Channels` array is empty

## Q: How to verify configuration is correct?

**A**: You can verify in the following ways:

1. **View System Logs**: After starting Edge Agent, check logs for configuration loading errors
2. **Check Connection Status**: 
   ```bash
   curl http://localhost:8001/api/DataAcquisition/GetPLCConnectionStatus
   ```
   If configuration is correct, you should see device connection status

3. **Check Metrics Data**: 
   ```bash
   curl http://localhost:8000/api/metrics-data
   ```
   If acquisition has started, you should see acquisition-related metrics

4. **Use Configuration Example**: Refer to `TEST_PLC.json` in the project as a configuration template

## Q: What happens if batch read configuration is incorrect?

**A**: Incorrect batch read configuration may cause:

1. **Data Read Errors**: If `BatchReadLength` is set too small, may not read all data points
2. **Index Errors**: If `Index` in `DataPoints` is configured incorrectly, may read wrong data
3. **Performance Degradation**: If batch read should be used but not enabled, will cause multiple network requests, degrading performance

**Configuration Recommendations**:
- If data points are consecutive, recommend enabling `EnableBatchRead` and correctly configuring `BatchReadRegister` and `BatchReadLength`
- `Index` should correspond to the position of data points in batch read results (note byte count occupied by data types)
- If unsure, first disable batch read and read registers one by one for testing

## Q: How to check if the system is running normally?

**A**: You can check in the following ways:

1. **Check Service Status**:
   ```bash
   # Central API
   curl http://localhost:8000/health
   
   # Edge Agent
   curl http://localhost:8001/api/DataAcquisition/GetPLCConnectionStatus
   ```

2. **View Monitoring Metrics**:
   ```bash
   curl http://localhost:8000/api/metrics-data
   ```
   Focus on the following metrics:
   - `data_acquisition_collection_rate`: Collection rate, should be greater than 0
   - `data_acquisition_errors_total`: Total errors, should be 0 or very few
   - `data_acquisition_connection_duration_seconds`: Connection duration

3. **View Web Interface**: Visit http://localhost:3000 to view visualized system status

4. **Check Logs**: Check for error logs, normal operation should mainly have Information level logs

## Return

- Return to [README](../README.en.md) to view project overview and documentation navigation
