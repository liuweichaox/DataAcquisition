# PLC 数据采集系统

## 📌 项目概述

本项目旨在通过动态收集来自 PLC（可编程逻辑控制器）的数据，为用户提供实时监控和分析工业设备运行状态的能力。支持多种 PLC 类型、实时数据采集、消息队列、高效数据存储等功能，适用于工业自动化过程中的监控与控制、设备性能分析及故障诊断。

---

## 🚀 核心功能

- **高效通讯**：基于 Modbus TCP 协议，实现稳定的高效通讯
- **消息队列**：支持数据缓存至 RabbitMQ、Kafka 或 本地消息队列，用于高并发数据采集
- **数据存储**：支持存储至本地 SQLite 数据库或云存储
- **日志记录**：支持自定义日志记录方式，便于问题排查与系统监控
- **多 PLC 数据采集**：支持从多个 PLC 周期性地采集实时数据
- **错误处理**：支持断线重连与超时重试，确保系统稳定运行
- **频率控制**：可配置采集频率，支持毫秒级控制
- **动态配置**：通过配置定义采集表、列名、频率，支持自定义数据点和采集方式
- **多平台支持**：兼容 .NET Standard 2.0 和 2.1
- **操作系统**：支持 Windows、Linux、macOS

---

## 🛠️ 安装与使用

### 1️⃣ 克隆仓库

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
```

### 2️⃣ 配置文件

在 `Configs` 文件夹中，每个表对应一个独立的 JSON 文件，您可以根据需要修改配置。配置文件定义了 PLC 信息、寄存器地址、数据类型等内容。

**示例配置** (`Configs/M01_Metrics.json`):

```json
{
  "Id": "ad3a076e-3d26-4b87-9d8d-24b42ab0cd3c",
  "IsEnabled": true,
  "DatabaseName": "dbo",
  "TableName": "m01_metrics",
  "CollectIntervalMs": 100,
  "HeartbeatIntervalMs": 5000,
  "BatchSize": 100,
  "Plc": {
    "Code": "M01",
    "IpAddress": "192.168.1.1",
    "Port": 502,
    "Registers": [
      {
        "ColumnName": "实时速度",
        "DataAddress": "D6000",
        "DataLength": 1,
        "DataType": "float",
        "EvalExpression": "value / 1000.0"
      }
    ]
  }
}
```

#### 配置文件解析

- **Id**: 唯一标识符，用于区分不同的配置。
- **IsEnabled**：启用该配置项，`true` 为启用，`false` 为禁用。
- **DatabaseName**：数据存储的数据库名称。
- **TableName**：存储数据的数据库表名。
- **CollectIntervalMs**：数据采集的频率，单位为毫秒。
- **HeartbeatIntervalMs**：心跳检测的频率，单位为毫秒。
- **BatchSize**：每次批量存储的数据量。
- **Plc**：PLC 配置信息，定义 PLC 的 IP 地址、端口和数据寄存器。
  - **Code**：PLC 的唯一代码。
  - **IpAddress**：PLC 的 IP 地址。
  - **Port**：PLC 的端口号，通常为 `502`（Modbus TCP 默认端口）。
  - **Registers**：包含要采集的数据寄存器的信息。
    - **ColumnName**：对应数据库中的列名。
    - **DataAddress**：寄存器地址。
    - **DataLength**：数据长度。
    - **DataType**：数据类型，支持 `ushort`,`uint`,`ulong`, `short`,`int`,`long`,`float`,`double`,`string`，`bool`。
    - **EvalExpression**：可选的表达式字段，用于对数据进行处理或转换。value 为寄存器读取到的值。

---

## ⚙️ 配置 `DataAcquisitionService`

在 `Startup.cs` 中配置 `IDataAcquisitionService` 实例，负责管理数据采集任务。

```csharp
builder.Services.AddSingleton<IDataAcquisitionService>(provider =>
{
    var hubContext = provider.GetService<IHubContext<DataHub>>();
    var dataAcquisitionConfigService = new DataAcquisitionConfigService(); // 配置服务：负责读取和解析数据采集配置（例如，频率、数据存储等）
    var plcClientFactory = new PlcClientFactory(); // PLC 客户端工厂：初始化 PLC 客户端，通过 IP 地址和端口连接到 PLC
    var dataStorageFactory = new DataStorageFactory(); // 数据存储工厂：初始化数据存储服务，用于采集的数据存储到数据库，支持本地存储和云存储
    var queueManagerFactory = new QueueManagerFactory(); // 消息队列管理器工厂：初始化消息队列管理器，支持 RabbitMQ 或 Kafka
    var messageService = new MessageService(hubContext); // 消息服务：负责处理数据采集异常消息，支持日志记录或报警
    return new DataAcquisitionService(
        dataAcquisitionConfigService,
        plcClientFactory,
        dataStorageFactory,
        queueManagerFactory,
        messageService);
});
```

### 配置解释

- **`IDataAcquisitionConfigService dataAcquisitionConfigService`**：配置服务，负责读取和解析数据采集的配置文件（例如，采集频率、数据存储方式等）。
- **`IPlcClientFactory plcClientFactory`**：初始化 PLC 客户端，通过 IP 地址和端口连接到 PLC。
- **`IDataStorageFactory dataStorageFactory`**：初始化据存储服务，用于采集的数据存储到数据库，支持本地存储和云存储。
- **`IQueueManagerFactory queueManagerFactory`**：初始化消息队列管理器，支持 RabbitMQ 或 Kafka。
- **`IMessageService messageService`**： 数据采集异常消息处理委托，可以用于日志记录或报警。

---

## 📡 数据采集控制器

在 `DataAcquisitionController` 中定义 API 接口，控制数据采集任务的开始与停止：

```csharp
/// <summary>
/// 数据采集控制器
/// </summary>
/// <param name="dataAcquisitionService"></param>
[Route("api/[controller]/[action]")]
public class DataAcquisitionController(IDataAcquisitionService dataAcquisitionService) : ControllerBase
{
    /// <summary>
    /// 开始数据采集任务
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public IActionResult StartCollectionTasks()
    {
         dataAcquisitionService.StartCollectionTasks();
         return Ok("开始数据采集任务");
    }
    
    /// <summary>
    /// 停止数据采集任务
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public IActionResult StopCollectionTasks()
    {
        dataAcquisitionService.StopCollectionTasks();
        return Ok("停止数据采集任务");
    }

    /// <summary>
    /// 获取 PLC 连接状态
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public IActionResult GetPlcConnectionStatus()
    {
        var plcConnectionStatus = dataAcquisitionService.GetPlcConnectionStatus();
        return Ok(plcConnectionStatus);
    }
}
```

---

## 📑 API 文档

### 开始数据采集任务

- **POST** `/api/DataAcquisition/StartCollectionTasks`
- **返回**：`开始数据采集任务`

### 停止数据采集任务

- **POST** `/api/DataAcquisition/StopCollectionTasks`
- **返回**：`停止数据采集任务`

### 获取 PLC 连接状态

- **GET** `/api/DataAcquisition/GetPlcConnectionStatus`
- **返回**：`PLC 连接状态字典`

---

## 🤝 贡献

如果您想为该项目贡献代码，欢迎提交 Pull Request！在提交之前，请确保代码通过了所有单元测试，并且没有引入任何破坏性变化。

## 📄 许可

本项目使用 MIT 许可证，详情请参阅 [LICENSE](LICENSE) 文件。

---

感谢您使用 PLC 数据采集系统！如有问题，欢迎提出 issue 或进行讨论。 🎉

---
