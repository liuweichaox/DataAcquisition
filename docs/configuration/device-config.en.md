# Device Configuration Guide

## Device Configuration Example

```json
{
  "IsEnabled": true,
  "PLCCode": "PLC01",
  "Host": "192.168.1.100",
  "Port": 502,
  "Type": "Mitsubishi",
  "HeartbeatMonitorRegister": "D100",
  "HeartbeatPollingInterval": 5000,
  "Channels": [
    {
      "Measurement": "temperature",
      "ChannelCode": "PLC01C01",
      "BatchSize": 10,
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Conditional",
      "EnableBatchRead": true,
      "BatchReadRegister": "D200",
      "BatchReadLength": 20,
      "DataPoints": [
        {
          "FieldName": "temp_value",
          "Register": "D200",
          "Index": 0,
          "DataType": "short",
          "EvalExpression": "value * 0.1"
        }
      ],
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

## Detailed Device Configuration Properties

### Root Level Properties

| Property Name              | Type      | Required | Description                                              |
| -------------------------- | --------- | -------- | -------------------------------------------------------- |
| `IsEnabled`                | `boolean` | Yes      | Whether the device is enabled                            |
| `PLCCode`                  | `string`  | Yes      | Unique identifier for the PLC device                     |
| `Host`                     | `string`  | Yes      | IP address of the PLC device                             |
| `Port`                     | `integer` | Yes      | Communication port of the PLC device                     |
| `Type`                     | `string`  | Yes      | PLC device type (e.g., Mitsubishi, Siemens, etc.)        |
| `HeartbeatMonitorRegister` | `string`  | No       | Register address for PLC heartbeat monitoring            |
| `HeartbeatPollingInterval` | `integer` | No       | Polling interval for heartbeat monitoring (milliseconds) |
| `Channels`                 | `array`   | Yes      | List of data acquisition channel configurations          |

### Channels Array Properties

| Property Name            | Type      | Required | Description                                                                                     |
| ------------------------ | --------- | -------- | ----------------------------------------------------------------------------------------------- |
| `Measurement`            | `string`  | Yes      | Measurement name in time-series database (table name)                                           |
| `ChannelCode`            | `string`  | Yes      | Unique identifier for the acquisition channel                                                   |
| `BatchSize`              | `integer` | No       | Number of data points to write to database in batch                                             |
| `AcquisitionInterval`    | `integer` | Yes      | Data acquisition interval (milliseconds)                                                        |
| `AcquisitionMode`        | `string`  | Yes      | Acquisition mode (Always: continuous acquisition, Conditional: conditional trigger acquisition) |
| `EnableBatchRead`        | `boolean` | No       | Whether to enable batch read functionality                                                      |
| `BatchReadRegister`      | `string`  | No       | Start register address for batch read                                                           |
| `BatchReadLength`        | `integer` | No       | Number of registers to read in batch                                                            |
| `DataPoints`             | `array`   | Yes      | List of data point configurations                                                               |
| `ConditionalAcquisition` | `object`  | No       | Conditional acquisition configuration (required only when AcquisitionMode is Conditional)       |

### DataPoints Array Properties

| Property Name    | Type      | Required | Description                                                                   |
| ---------------- | --------- | -------- | ----------------------------------------------------------------------------- |
| `FieldName`      | `string`  | Yes      | Field name in time-series database                                            |
| `Register`       | `string`  | Yes      | PLC register address for the data point                                       |
| `Index`          | `integer` | No       | Index position in batch read results                                          |
| `DataType`       | `string`  | Yes      | Data type (e.g., short, int, float, etc.)                                     |
| `EvalExpression` | `string`  | No       | Data conversion expression (use 'value' variable to represent original value) |

### ConditionalAcquisition Object Properties

| Property Name      | Type     | Required | Description                                                                                                    |
| ------------------ | -------- | -------- | -------------------------------------------------------------------------------------------------------------- |
| `Register`         | `string` | Yes      | Register address for conditional trigger monitoring                                                            |
| `DataType`         | `string` | Yes      | Data type of the conditional trigger register                                                                  |
| `StartTriggerMode` | `string` | Yes      | Start acquisition trigger mode (RisingEdge: trigger on value increase, FallingEdge: trigger on value decrease) |
| `EndTriggerMode`   | `string` | Yes      | End acquisition trigger mode (RisingEdge: trigger on value increase, FallingEdge: trigger on value decrease)   |

## AcquisitionTrigger Mode Description

| Trigger Mode  | Description                                |
| ------------- | ------------------------------------------ |
| `RisingEdge`  | Trigger when value increases (prev < curr) |
| `FallingEdge` | Trigger when value decreases (prev > curr) |

> Note: The RisingEdge and FallingEdge here are different from traditional edge triggering (0→1 or 1→0). They are triggered based on value increases/decreases, not strict 0/1 transitions.

## Configuration File Location

Device configuration files are stored in the `src/DataAcquisition.Edge.Agent/Configs/` directory, format is `*.json`. The system automatically monitors configuration file changes in this directory and supports hot reload without service restart.

## Related Documentation

- [Edge Agent Application Configuration](./edge-agent-config.en.md)
- [Configuration to Database Mapping](./database-mapping.en.md)
