# 动态 PLC 数据采集

### 配置 PLC 通讯地址

```json
[
  {
    "Code": "M11C01",
    "IpAddress": "192.168.1.3",
    "Port": 502
  }
]
```

### 配置 PLC 采集表、列、采集频率

```json
{
  "Id": 1,
  "IsEnabled": true,
  "TableName": "device_monitor",
  "CollectionFrequency": 10,
  "DatabaseName": "192.168.0.110",
  "MetricConfigs": [
    {
      "Id": 1,
      "MetricId": 1,
      "ColumnName": "温度",
      "DataAddress": "D6000",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "Id": 1,
      "MetricId": 1,
      "ColumnName": "压力",
      "DataAddress": "D6100",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "Id": 1,
      "MetricId": 1,
      "ColumnName": "光栅位置",
      "DataAddress": "D6200",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "Id": 1,
      "MetricId": 1,
      "ColumnName": "伺服位置",
      "DataAddress": "D6300",
      "DataLength": 1,
      "DataType": "float"
    }
  ]
}
```
