# 动态 PLC 数据采集系统

## 项目概述

### 目标

本项目旨在通过动态收集来自PLC（可编程逻辑控制器）的数据，为用户提供实时监控和分析工业设备运行状态的能力。

### 技术栈

- 编程语言：C#
- 通信协议：Modbus TCP/IP

## 主要功能

- **数据采集**：支持从多个PLC设备中获取多种类型的实时数据。
- **配置灵活性**：允许用户自定义数据点、采集频率以及采集方式。
- **实时监控**：具备数据可视化功能，能够即时展示设备状态的变化。

## 适用场景

- 工业自动化过程中的监控与控制
- 设备性能分析及故障诊断
- 历史数据记录与回溯

## 使用指南

### 配置 PLC 通讯地址

#### 文件路径

`Configs/devices.json`

#### 样例配置

```json
[
  {
    "Code": "S00001",
    "IpAddress": "192.168.1.100",
    "Port": 502
  }
]
```

### 设置 PLC 数据采集参数

#### 配置目录

`Configs/MetricConfigs` （每个表对应一个独立的JSON文件）

#### 参数详解

- `IsEnabled`: 是否启用此表的数据采集
- `TableName`: 数据库中的表名
- `CollectionFrequency`: 数据采集的时间间隔（毫秒）
- `DatabaseName`: 存储数据的目标数据库名称
- `MetricColumnConfigs`: 每个指标的具体配置
  - `ColumnName`: 在数据库表中的列名
  - `DataAddress`: PLC中存储该数据的地址
  - `DataLength`: 读取的数据长度
  - `DataType`: 数据类型

#### 样例配置 

`Configs/MetricConfigs/rocket_flight_metrics.json`

```json
{
  "IsEnabled": true,
  "TableName": "rocket_flight_metrics",
  "CollectionFrequency": 100,
  "DatabaseName": "dbo",
  "MetricColumnConfigs": [
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

### 自定义服务接口

#### PLC 通讯服务

实现 `Services/IPLCCommunicator` 接口，确保每个PLC设备都有单独的连接，并支持断线自动重连和读取失败后的重试机制。

#### 数据存储服务

实现 `Services/IDataStorage` 接口，利用 `BlockingCollection<T>` 来管理多线程环境下的数据流，保证高效的数据处理与持久化到不同类型的数据库中。
