# 动态 PLC 数据采集系统

## 1. 项目概述

本项目旨在通过动态收集来自 PLC（可编程逻辑控制器）的数据，为用户提供实时监控和分析工业设备运行状态的能力。

## 2. 技术栈

- **编程语言**：C# / .NET 8
- **通信协议**：Modbus TCP/IP

## 3. 项目结构介绍
```
DynamicPLCDataCollector/
├── /src                                      # 源代码目录
│   ├── /Common                               # 工具类
│   ├── /Configs                              # 配置文件
│   ├── /Extensions                           # 扩展方法
│   ├── /Services                             # 服务相关的代码
│   │   ├── /DataStorages                     # 数据服务 (与数据获取、存储相关)
│   │   │   ├── IDataStorage.cs               # 数据服务接口
│   │   │   └── SQLiteDataStorage.cs          # SQLite 数据服务实现
│   │   ├── /Devices                          # 设备服务
│   │   │   ├── IDeviceService.cs             # 设备服务接口
│   │   │   └── DataProcessingService.cs      # 设备服务实现
│   │   ├── /MetricTableConfigs               # 数据采集配置服务
│   │   │   ├── IMetricTableConfigService.cs  # 数据采集配置服务接口
│   │   │   └── MetricTableConfigService.cs   # 数据采集配置服务实现
│   │   ├── /PLCClients                       # PLC 通信服务 (与 PLC 通信)
│   │   │   ├── IPLCClient.cs                 # PLC 通信服务接口
│   │   │   └── PLCClient.cs                  # PLC 通信服务实现
│   │   └── /QueueManagers                    # 队列管理器服务
│   │       ├── IQueueManager.cs              # 队列管理器服务接口
│   │       └── QueueManager.cs               # 队列管理器服务实现
│   ├── /Models                               # 数据模型
│   ├── /Utils                                # 辅助函数
│   ├── DynamicPLCDataCollector.csproj        # 项目文件
│   ├── DynamicPLCDataCollector.sln           # 解决方案
│   ├── DataCollector.cs                      # 配置文件
│   ├── Program.cs                            # 程序主文件
│   └── Usings.cs                             # 全局 using
├── .gitignore                                # Git 忽略文件
└── README.md                                 # 项目说明文件
```

## 4. 主要功能

- **数据采集**：支持从多个 PLC 设备获取多种类型的实时数据。
- **配置灵活性**：用户可自定义数据点、采集频率及方式。
- **实时监控**：具备数据可视化功能，能够即时展示设备状态变化。

## 5. 适用场景

- 工业自动化过程中的监控与控制
- 设备性能分析及故障诊断
- 历史数据记录与回溯

## 6. 使用指南

### 6.1 实现 IPLClient 接口（定义 PLC 客户端类型）

`IPLClient` 是 PLC 客户端接口，项目默认使用 `HslCommunication` 库实现，用户可根据需求自行替换。

```C#
/// <summary>
/// PLC 客户端实现
/// </summary>
public class PLCClient : IPLCClient
{
    private readonly InovanceTcpNet _plcClient;

    public PLCClient(string ipAddress, int port)
    {
        // 初始化 PLC 客户端
    }

    public async Task<OperationResult<bool>> ConnectServerAsync()
    {
        // 连接到 PLC 服务器并返回连接结果
    }

    public async Task<OperationResult<bool>> ConnectCloseAsync()
    {
        // 断开与 PLC 的连接并返回断开结果
    }

    public bool IsConnected()
    {
        // 检查当前连接状态，返回布尔值
    }

    // 其他读取方法...
}
```

### 6.2 实现 AbstractDataStorage 抽象类（定义持久化数据库类型）

`IDataStorage` 为数据存储服务，内部使用 `BlockingCollection<T>` 管理多线程环境下的数据流，确保高效数据处理及持久化。数据每次读取会添加到队列。
这里为了提高插入效率使用是批量插入，如果不需要批量插入，可以修改`MetricTableConfig`中`BatchSize`配置值为`1`，即可实现单条插入。

```C#
// <summary>
/// SQLite 数据存储实现
/// </summary>
public class SQLiteDataStorage : AbstractDataStorage
{
    private readonly SqliteConnection _connection;
    private readonly Device _device;
    private readonly MetricTableConfig _metricTableConfig;
    public SQLiteDataStorage(Device device, MetricTableConfig metricTableConfig):base(device, metricTableConfig)
    {
        _device = device;
        _metricTableConfig = metricTableConfig;
            
        var dbPath = Path.Combine(AppContext.BaseDirectory, $"{metricTableConfig.DatabaseName}.sqlite"); 
        _connection = new SqliteConnection($@"Data Source={dbPath};");
        _connection.Open();
    }

    public async Task SaveBatchAsync(List<Dictionary<string, object>> data)
    {
        await _connection.InsertBatchAsync(_metricTableConfig.TableName, data);
    }

    public override async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
```

### 6.3 配置 PLC 通讯地址（定义 PLC 服务连接方式）

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

### 6.4 设置 PLC 数据采集参数（定义怎么采集数据）

**文件路径**：`Configs/MetricConfigs`（每个表对应一个独立的 JSON 文件）

**参数详解**：

- `IsEnabled`：是否启用此表的数据采集
- `DatabaseName`：存储数据的目标数据库名称，可以根据数据库名称进行分库存储
- `TableName`：数据库表名
- `CollectionFrequency`：数据采集间隔（毫秒）
- `BatchSize`: 批量保存大小
- `MetricColumnConfigs`：指标的具体配置
  - `ColumnName`：数据库表中的列名
  - `DataAddress`：PLC 中存储该数据的地址
  - `DataLength`：读取的数据长度
  - `DataType`：数据类型

**样例配置**：`Configs/MetricConfigs/rocket_flight_metrics.json`

```json
{
  "IsEnabled": true,
  "DatabaseName": "dbo",
  "TableName": "rocket_flight_metrics",
  "CollectionFrequency": 1000,
  "BatchSize": 100,
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

### 6.5 运行
使用自定义的`PLCClient`，`SQLiteDataStorage`类`IDataStorage` 构建 `DataCollector`实例，运行 `StartCollectionTasks` 函数，即可开启数据采集。
```C#
IPLCClient PLCClientFactory(string ipAddress, int port) => new PLCClient(ipAddress, port);

IDataStorage DataStorageFactory(MetricTableConfig metricTableConfig) => new SQLiteDataStorage(metricTableConfig);

var dataCollector = new DataCollector(PLCClientFactory, DataStorageFactory);

await dataCollector.StartCollectionTasks();

await Task.Delay(Timeout.Infinite);
```

## 7. 总结

本动态 PLC 数据采集系统通过灵活配置和强大功能，能有效支持工业自动化过程中的数据监控与分析，适用于多种场景。用户可根据实际需求进行定制与扩展，提升生产效率和设备管理能力。
