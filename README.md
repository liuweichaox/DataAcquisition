# PLC 数据采集系统

## 1. 项目概述

本项目旨在通过动态收集来自 PLC（可编程逻辑控制器）的数据，为用户提供实时监控和分析工业设备运行状态的能力。

## 2. 技术栈

- **编程语言**：C#
- **通信协议**：Modbus TCP/IP

## 3. 主要功能和特点
-	**动态配置：** 通过配置文件定义采集表、列名、频率，支持自定义数据点和采集方式。
- **多平台支持：** 兼容 .NET Standard 2.0 和 .NET Standard 2.1。
-	**多 PLC 数据采集：** 支持同时从多个 PLC 周期性地采集实时寄存器数据。
-	**模块化设计：** 易于扩展和维护。
-	**高效通讯：** 基于 Modbus TCP 协议，实现稳定的高效通讯。
-	**数据存储：** 支持存储至本地文件或数据库，云存储。
-	**频率控制：** 可配置采集频率，支持毫秒级控制。
-	**错误处理：** 支持断线重连与超时重试，确保系统稳定运行。
- **消息队列：** 支持消息队列，实现高并发数据采集。可使用 RabbitMQ 或 Kafka。

## 4. 适用场景

- 工业自动化过程中的监控与控制
- 设备性能分析及故障诊断
- 历史数据记录与回溯

## 5. 使用示例
### 5.1 实现 IDataAcquisitionConfigService 接口（定义采集配置）
#### 5.1.1 设置 PLC 数据采集参数（定义怎么采集数据）

**文件路径**：`Configs/MetricConfigs`（每个表对应一个独立的 JSON 文件）

**参数详解**：
- `IsEnabled`：是否启用此表的数据采集
- `Code`: 数据采集表编码
- `IpAddress`: PLC 的 IP 地址
- `Port`: PLC 的端口号
- `DatabaseName`：存储数据的目标数据库名称，可以根据数据库名称进行分库存储
- `TableName`：数据库表名
- `CollectionFrequency`：数据采集间隔（毫秒）
- `BatchSize`: 批量保存大小
- `PositionConfigs`：指标的具体配置
  - `ColumnName`：数据库表中的列名
  - `DataAddress`：PLC 中存储该数据的地址
  - `DataLength`：读取的数据长度
  - `DataType`：数据类型
  - `EvalExpression`：数据采集时的计算表达式，支持对数据进行处理和计算，如 `value / 1000.0`，value 为采集到的数据

**样例配置**：`Configs/MetricConfigs/rocket_flight_metrics.json`

```json
{
  "IsEnabled": true,
  "Code": "M01",
  "IpAddress": "192.168.1.1",
  "Port": 502,
  "DatabaseName": "dbo",
  "TableName": "m01_metrics",
  "CollectionFrequency": 1000,
  "BatchSize": 100,
  "PositionConfigs": [
    {
      "ColumnName": "实时速度",
      "DataAddress": "D6000",
      "DataLength": 1,
      "DataType": "float",
      "EvalExpression":  null
    },
    {
      "ColumnName": "实时高度",
      "DataAddress": "D6100",
      "DataLength": 1,
      "DataType": "float",
      "EvalExpression":  null
    },
    {
      "ColumnName": "加速度",
      "DataAddress": "D6200",
      "DataLength": 1,
      "DataType": "float",
      "EvalExpression":  null
    },
    {
      "ColumnName": "气动压力",
      "DataAddress": "D6300",
      "DataLength": 1,
      "DataType": "float",
      "EvalExpression":  "value / 1000.0"
    },
    {
      "ColumnName": "编号",
      "DataAddress": "D6400",
      "DataLength": 10,
      "DataType": "string",
      "EvalExpression":  null
    }
  ]
}
```
#### 5.1.2 实现`IDataAcquisitionConfigService`接口
```C#
public class DataAcquisitionConfigService : IDataAcquisitionConfigService
{
    public async Task<List<DataAcquisitionConfig>> GetDataAcquisitionConfigs()
    {
        var dataAcquisitionConfigs = await JsonUtils.LoadAllJsonFilesAsync<DataAcquisitionConfig>("Configs");
        return dataAcquisitionConfigs;
    }
}
```
### 5.2 实现 IPLClient 接口（定义 PLC 客户端类型）

`IPLClient` 是 PLC 客户端接口，示例项目使用 `HslCommunication` 库实现，用户可根据不同 PLC 实现。

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

### 5.3 实现 AbstractQueueManager 抽象类（定义消息队列类型）

`IQueueManager` 为消息队列服务，确保高效数据处理及持久化。数据每次读取会添加到队列。用户可根据需要选择不同的消息队列实现，如 RabbitMQ 或 Kafka。这里使用 BlockingCollection 作为示例。

```C#
public class QueueManager : AbstractQueueManager
{
    private readonly BlockingCollection<Dictionary<string, object>> _queue;
    private readonly IDataStorage _dataStorage;
    private readonly List<Dictionary<string, object>> _dataBatch;
    private readonly DataAcquisitionConfig _dataAcquisitionConfig;

    public QueueManager(DataStorageFactory dataStorageFactory, DataAcquisitionConfig dataAcquisitionConfig) : base(
        dataStorageFactory, dataAcquisitionConfig)
    {
        _queue = new BlockingCollection<Dictionary<string, object>>();
        _dataStorage = dataStorageFactory(dataAcquisitionConfig);
        _dataAcquisitionConfig = dataAcquisitionConfig;
        _dataBatch = new List<Dictionary<string, object>>();
    }

    public override async Task ProcessQueue()
    {
        foreach (var data in _queue.GetConsumingEnumerable())
        {
            _dataBatch.Add(data);

            if (_dataBatch.Count >= _dataAcquisitionConfig.BatchSize)
            {
                await _dataStorage.SaveBatchAsync(_dataBatch);
                _dataBatch.Clear();
            }
        }

        if (_dataBatch.Count > 0)
        {
            await _dataStorage.SaveBatchAsync(_dataBatch);
        }
    }

    public override void EnqueueData(Dictionary<string, object> data)
    {
        _queue.Add(data);
    }

    public override void Complete()
    {
        _queue.CompleteAdding();
        _dataStorage.DisposeAsync();
    }
}
```

### 5.4 实现 AbstractDataStorage 抽象类（定义持久化数据库类型）

`IDataStorage` 为数据存储服务，确保数据持久化。用户可根据不同数据库实现，如 MySQL、PostgreSQL 或 InflixDB 等数据库，这里以 SQLite 为例。
这里为了提高插入效率使用是批量插入，如果不需要批量插入，可以修改`DataAcquisitionConfig`中`BatchSize`配置值为`1`，即可实现单条插入。

```C#
/// <summary>
/// SQLite 数据存储实现
/// </summary>
public class SQLiteDataStorage : AbstractDataStorage
{
    private readonly SqliteConnection _connection;
    public SQLiteDataStorage(DataAcquisitionConfig config) : base(config)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, $"{config.DatabaseName}.sqlite");
        _connection = new SqliteConnection($@"Data Source={dbPath};");
        _connection.Open();
    }

    public override async Task SaveBatchAsync(List<Dictionary<string, object>> data)
    {
        await _connection.InsertBatchAsync(DataAcquisitionConfig.TableName, data);
    }

    public override async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
```

### 5.5 运行
构建 `IDataAcquisitionService`实例
#### 构造函数参数说明

`dataAcquisitionConfigService：` 数据采集配置服务实例

`plcClientFactory：` PLC 客户端服务创建

`dataStorageFactory：` 数据存储服务创建

`processReadData：` 读取到后执行的委托，可以在此对读取到的数据进行拓展或额外处理。

#### 函数说明

`StartCollectionTasks` 函数，开启数据采集。

`StopCollectionTasks` 函数，停止数据采集。

#### 示例
```C#
var dataAcquisitionService = new DataAcquisitionService(
    dataAcquisitionConfigService,
    (ipAddress, port) => new PlcClient(ipAddress, port),
    config => new SQLiteDataStorage(config),
    (factory, config) => new QueueManager(factory, config),
    (data, config) =>
    {
        data["时间"] = DateTime.Now;
        data["DeviceCode"] = config.Code;
    });

await dataAcquisitionService.StartCollectionTasks();
```

## 6. 总结

本动态 PLC 数据采集系统通过灵活配置和强大功能，能有效支持工业自动化过程中的数据监控与分析，适用于多种场景。用户可根据实际需求进行定制与扩展，提升生产效率和设备管理能力。
