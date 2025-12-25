# 🔌 API Usage Examples

This document introduces how to use the APIs provided by the DataAcquisition system.

## Metrics Data Query

```bash
# Get Prometheus format metrics
curl http://localhost:8000/metrics

# Get JSON format metrics
curl http://localhost:8000/api/metrics-data
```

## PLC Connection Status Query

```bash
curl http://localhost:8001/api/DataAcquisition/GetPLCConnectionStatus
```

## PLC Write Operation

```csharp
var request = new PLCWriteRequest
{
    PLCCode = "M01C123",
    Items = new List<PLCWriteItem>
    {
        new PLCWriteItem
        {
            Address = "D300",
            DataType = "short",
            Value = 100
        }
    }
};

var response = await httpClient.PostAsJsonAsync("/api/DataAcquisition/WriteRegister", request);
```

For more API examples, see: [Chinese API Usage Guide](api-usage.md)
