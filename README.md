# 动态 PLC 数据采集

### 配置 PLC 通讯地址

配置文件：Configs/devices.json
#### 定义
- Code: 设备编码
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

#### 定义

- IsEnabled: 是否开启
- TableName: 表名
- CollectionFrequency: 数据采集频率
- DatabaseName: 数据库名称
- MetricConfigs 列名配置
  - ColumnName: 列名
  - DataAddress: PLC 数据地址
  - DataLength: PLC 数据长度
  - DataType: PLC 数据类型


配置目录：Configs/MetricConfigs (每张表对应的配置，表与 JSON 文件为一对一的关系)

示例文件：rocket_flight_metrics.json

```json
{
  "IsEnabled": true,
  "TableName": "rocket_flight_metrics",
  "CollectionFrequency": 100,
  "DatabaseName": "dbo",
  "MetricConfigs": [
    {
      "ColumnName": "实时速度",
      "DataAddress": "D6000",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "ColumnName": "实时高度",
      "DataAddress": "D6100",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "ColumnName": "加速度",
      "DataAddress": "D6200",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "ColumnName": "气动压力",
      "DataAddress": "D6300",
      "DataLength": 1,
      "DataType": "float"
    },
     {
      "ColumnName": "发动机推力",
      "DataAddress": "D6400",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "ColumnName": "重心变化",
      "DataAddress": "D6500",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "ColumnName": "轨道倾角",
      "DataAddress": "D6600",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "ColumnName": "飞行时间",
      "DataAddress": "D6700",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "ColumnName": "温度监测",
      "DataAddress": "D6800",
      "DataLength": 1,
      "DataType": "float"
    },
    {
      "ColumnName": "姿态控制",
      "DataAddress": "D6900",
      "DataLength": 1,
      "DataType": "float"
    }
  ]
}
```
