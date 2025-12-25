# Configuration to Database Mapping

The system maps configuration files to InfluxDB time-series database. Here's the mapping relationship:

## Mapping Table

| Configuration Field                 | InfluxDB Structure      | Description                                | Example                      |
| ----------------------------------- | ----------------------- | ------------------------------------------ | ---------------------------- |
| `Channels[].Measurement`            | **Measurement**         | Measurement name (table name)              | `"sensor"`                   |
| `PLCCode`                           | **Tag**: `plc_code`     | PLC device code tag                        | `"M01C123"`                  |
| `Channels[].ChannelCode`            | **Tag**: `channel_code` | Channel code tag                           | `"M01C01"`                   |
| `EventType`                         | **Tag**: `event_type`   | Event type tag (Start/End/Data)            | `"Start"`, `"End"`, `"Data"` |
| `Channels[].DataPoints[].FieldName` | **Field**               | Data field name                            | `"up_temp"`, `"down_temp"`   |
| `CycleId`                           | **Field**: `cycle_id`   | Acquisition cycle unique identifier (GUID) | `"guid-xxx"`                 |
| Acquisition time                    | **Timestamp**           | Data point timestamp                       | `2025-01-15T10:30:00Z`       |

## Configuration Example and Line Protocol

### Configuration File Example (`M01C123.json`)

```json
{
  "PLCCode": "M01C123",
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "M01C01",
      "DataPoints": [
        {
          "FieldName": "up_temp",
          "Register": "D6002",
          "Index": 2,
          "DataType": "short"
        },
        {
          "FieldName": "down_temp",
          "Register": "D6004",
          "Index": 4,
          "DataType": "short",
          "EvalExpression": "value / 1000.0"
        }
      ],
      "ConditionalAcquisition": {
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

### Generated InfluxDB Line Protocol

#### Start Event (conditional acquisition start)

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Start up_temp=250i,down_temp=0.18,cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312200000000000
```

#### Data Event (normal data point)

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=Data up_temp=255i,down_temp=0.19 1705312210000000000
```

#### End Event (conditional acquisition end)

```
sensor,plc_code=M01C123,channel_code=M01C01,event_type=End cycle_id="550e8400-e29b-41d4-a716-446655440000" 1705312300000000000
```

## Line Protocol Format Explanation

InfluxDB Line Protocol format:

```
measurement,tag1=value1,tag2=value2 field1=value1,field2=value2 timestamp
```

### Field Type Explanation

- **Measurement**: From configuration `Measurement`, e.g., `"sensor"`
- **Tags** (for filtering and grouping, indexed fields):
  - `plc_code`: PLC device code
  - `channel_code`: Channel code
  - `event_type`: Event type (`Start`/`End`/`Data`)
- **Fields** (actual data values):
  - All fields from `DataPoints[].FieldName` (e.g., `up_temp`, `down_temp`)
  - `cycle_id`: Conditional acquisition cycle ID (GUID, used to link Start/End events)
  - Numeric types: integers use `i` suffix (e.g., `250i`), floats are written directly (e.g., `0.18`)
- **Timestamp**: Data acquisition time (nanosecond precision)

## Query Examples

### Query data from a specific PLC channel within a specified time range (1h)

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["plc_code"] == "M01C123")
  |> filter(fn: (r) => r["channel_code"] == "M01C01")
```

### Query a complete conditional acquisition cycle

```flux
from(bucket: "your-bucket")
  |> range(start: -1h)
  |> filter(fn: (r) => r["_measurement"] == "sensor")
  |> filter(fn: (r) => r["cycle_id"] == "550e8400-e29b-41d4-a716-446655440000")
```

## Related Documentation

- [Device Configuration Guide](./device-config.en.md)
- [Edge Agent Application Configuration](./edge-agent-config.en.md)
