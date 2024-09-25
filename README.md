# 动态 PLC 数据采集

### 配置 PLC 通讯地址

配置文件：Configs/devices.json
##### 定义
- Code:: 设备编码
- IpAddress: IP 地址
- Port： IP端口

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

##### 定义

- IsEnabled: 是否开启
- TableName: 表名
- CollectionFrequency: 数据采集频率
- DatabaseName: 数据库名称
- MetricConfigs 列名配置
  - ColumnName: 列名
  - DataAddress: PLC 数据地址
  - DataLength: PLC 数据长度
  - DataType: PLC 数据类型


配置文件：Configs/MetricConfig.json (每张表对应的配置，表与 JSON 文件为一对一的关系)

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
