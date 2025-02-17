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
  "IsEnabled": true,
  "DatabaseName": "dbo",
  "TableName": "m01_metrics",
  "CollectionFrequency": 1000,
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
        "EvalExpression": null
      }
    ]
  }
}
```

#### 配置文件解析

- **IsEnabled**：启用该配置项，`true` 为启用，`false` 为禁用。
- **DatabaseName**：数据存储的数据库名称。
- **TableName**：存储数据的数据库表名。
- **CollectionFrequency**：数据采集的频率，单位为毫秒。`1000` 表示每秒一次。
- **BatchSize**：每次批量存储的数据量。
- **Plc**：PLC 配置信息，定义 PLC 的 IP 地址、端口和数据寄存器。
  - **Code**：PLC 的唯一代码。
  - **IpAddress**：PLC 的 IP 地址。
  - **Port**：PLC 的端口号，通常为 `502`（Modbus TCP 默认端口）。
  - **Registers**：包含要采集的数据寄存器的信息。
    - **ColumnName**：对应数据库中的列名。
    - **DataAddress**：寄存器地址。
    - **DataLength**：数据长度。
    - **DataType**：数据类型，支持 `ushort`,`uint`,`ulong`, `short`,`int`,`long`,`float`,`double`,`string`，`boolean`。
    - **EvalExpression**：可选的表达式字段，用于对数据进行处理或转换。

---

## ⚙️ 配置 `DataAcquisitionService`

在 `Startup.cs` 中配置 `IDataAcquisitionService` 实例，负责管理数据采集任务。

```csharp
builder.Services.AddSingleton<IDataAcquisitionService>(provider =>
{
    // 获取 SignalR HubContext 用于推送实时数据到客户端
    var hubContext = provider.GetService<IHubContext<DataHub>>();

    // 返回一个新的 DataAcquisitionService 实例，构造时传入依赖项
    return new DataAcquisitionService(
        // 配置服务：负责读取和解析数据采集配置（例如，频率、数据存储等）
        new DataAcquisitionConfigService(),

        // PLC 客户端：通过 PLC 的 IP 地址和端口创建一个新的 PlcClient
        (ipAddress, port) => new PlcClient(ipAddress, port),

        // 数据存储：根据配置创建 SQLite 数据存储实例
        config => new SqLiteDataStorage(config),

        // 消息队列管理器：初始化 RabbitMQ 或 Kafka 的消息队列管理
        (factory, config) => new QueueManager(factory, config),

        // 数据预处理：为每个数据点添加时间戳，确保数据同步和时序分析
        (dataPoint, config) =>
        {
            dataPoint.Values["Timestamp"] = DateTime.Now;
        },

        // 推送数据到前端：将实时采集的数据通过 SignalR 推送给前端客户端
        async (message) =>
        {
            // 推送消息到所有连接的客户端
            await hubContext.Clients.All.SendAsync("ReceiveMessage", message);
        });
});
```

### 配置解释

- **`IHubContext<DataHub>`**：用来获取 SignalR 的上下文，SignalR 用于实现实时数据推送。
- **`DataAcquisitionConfigService`**：配置服务，负责读取和解析数据采集的配置文件（例如，采集频率、数据存储方式等）。
- **`(ipAddress, port) => new PlcClient(ipAddress, port)`**：初始化 PLC 客户端，通过 IP 地址和端口连接到 PLC。
- **`config => new SqLiteDataStorage(config)`**：初始化 SQLite 数据存储服务，确保采集的数据存储在数据库中。
- **`(factory, config) => new QueueManager(factory, config)`**：初始化消息队列管理器，支持 RabbitMQ 或 Kafka。
- **`(dataPoint, config) => { dataPoint.Values["Timestamp"] = DateTime.Now; }`**：为每个数据点添加时间戳，确保数据同步。
- **`async (message) => { await hubContext.Clients.All.SendAsync("ReceiveMessage", message); }`**：推送实时数据到前端客户端，通过 SignalR 进行实时通信。

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

---

## 🤝 贡献

如果您想为该项目贡献代码，欢迎提交 Pull Request！在提交之前，请确保代码通过了所有单元测试，并且没有引入任何破坏性变化。

## 📄 许可

本项目使用 MIT 许可证，详情请参阅 [LICENSE](LICENSE) 文件。

---

感谢您使用 PLC 数据采集系统！如有问题，欢迎提出 issue 或进行讨论。 🎉

---