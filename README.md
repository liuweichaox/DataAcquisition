# 动态 PLC 数据采集系统

## 1. 项目概述

本项目旨在通过动态收集来自 PLC（可编程逻辑控制器）的数据，为用户提供实时监控和分析工业设备运行状态的能力。

## 2. 技术栈

- **编程语言**：C#
- **通信协议**：Modbus TCP/IP

## 3. 主要功能

- **数据采集**：支持从多个 PLC 设备获取多种类型的实时数据。
- **配置灵活性**：用户可自定义数据点、采集频率及方式。
- **实时监控**：具备数据可视化功能，能够即时展示设备状态变化。

## 4. 适用场景

- 工业自动化过程中的监控与控制
- 设备性能分析及故障诊断
- 历史数据记录与回溯

## 5. 使用指南

### 5.1 配置 PLC 通讯地址

**文件路径**：`Configs/devices.json`

**样例配置**：

```json
[
  {
    "Code": "S00001",
    "IpAddress": "192.168.1.100",
    "Port": 502
  }
]
```

### 5.2 设置 PLC 数据采集参数

**文件路径**：`Configs/MetricConfigs`（每个表对应一个独立的 JSON 文件）

**参数详解**：

- `IsEnabled`：是否启用此表的数据采集
- `TableName`：数据库表名
- `CollectionFrequency`：数据采集间隔（毫秒）
- `DatabaseName`：存储数据的目标数据库名称
- `MetricColumnConfigs`：指标的具体配置
  - `ColumnName`：数据库表中的列名
  - `DataAddress`：PLC 中存储该数据的地址
  - `DataLength`：读取的数据长度
  - `DataType`：数据类型

**样例配置**：`Configs/MetricConfigs/rocket_flight_metrics.json`

```json
{
  "IsEnabled": true,
  "TableName": "rocket_flight_metrics",
  "CollectionFrequency": 1000,
  "BatchSize": 100,
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
      "ColumnName": "编号",
      "DataAddress": "D6400",
      "DataLength": 10,
      "DataType": "string"
    }
  ]
}
```

### 5.3 实现 IPLClient 接口

`IPLClient` 是 PLC 客户端接口，项目默认使用 `HslCommunication` 库实现，用户可根据需求自行替换。

### 5.4 实现 AbstractPLCClientManager 抽象类

`AbstractPLCClientManager` 为 `IPLCClient` 的管理器，负责创建单独的连接，并支持自动重连及读取失败后的重试机制。

```C#
    public class SQLiteDataStorage : AbstractDataStorage
    {
        protected override async void ProcessQueue(BlockingCollection<Dictionary<string, object>> queue, MetricTableConfig metricTableConfig)
        {
            await using var sqLiteConnection = new SQLiteConnection($@"Data Source=db.sqlite;Version=3;");
            sqLiteConnection.Open();

            var dataBatch = new List<Dictionary<string, object>>();

            foreach (var data in queue.GetConsumingEnumerable())
            {
                dataBatch.Add(data);

                if (dataBatch.Count >= metricTableConfig.BatchSize)
                {
                    await sqLiteConnection.InsertBatchAsync(metricTableConfig.TableName, dataBatch);
                    dataBatch.Clear();
                }
            }

            if (dataBatch.Count > 0)
            {
                await sqLiteConnection.InsertBatchAsync(metricTableConfig.TableName, dataBatch);
            }
        }
    }
```

### 5.5 实现 AbstractDataStorage 抽象类

`AbstractDataStorage` 为数据存储服务，使用 `BlockingCollection<T>` 管理多线程环境下的数据流，确保高效数据处理及持久化。

```C#
public class PLCClientManager : AbstractPLCClientManager
{
    public PLCClientManager(List<Device> devices) : base(devices)
    {
    }
    
    protected override OperationResult<IPLClient> CreatePLCClient(Device device)
    {
        var plcClient = new PLCClient(device.IpAddress, device.Port);

        var connect = plcClient.ConnectServerAsync().Result;
        if (connect.IsSuccess)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {device.Code} 成功！");
            return new OperationResult<IPLClient>(plcClient);
        }
        else
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {device.Code} 失败：{connect.Message}");
            return new OperationResult<IPLClient>(connect.Message);
        }
    }
}
```

### 5.6 运行
使用 `IPLCClientManager`、`IDataStorage` 实例构建 `DataCollector`，运行 `StartCollectionTasks` 函数，即可开启数据采集。
```C#
using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.PLCClients;
using DynamicPLCDataCollector.Utils;

Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集程序已启动...");

var devices = await JsonUtils.LoadConfigAsync<List<Device>>("Configs/devices.json");

var metricTableConfigs = await JsonUtils.LoadAllJsonFilesAsync<MetricTableConfig>("Configs/MetricConfigs");

IPLCClientManager clientManager = new PLCClientManager(devices);

IDataStorage dataStorage = new SQLiteDataStorage();

var dataCollector = new DataCollector(clientManager, dataStorage);

await dataCollector.StartCollectionTasks(devices, metricTableConfigs);
```

## 6. 总结

本动态 PLC 数据采集系统通过灵活配置和强大功能，能有效支持工业自动化过程中的数据监控与分析，适用于多种场景。用户可根据实际需求进行定制与扩展，提升生产效率和设备管理能力。