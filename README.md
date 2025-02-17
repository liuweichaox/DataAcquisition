# PLC 数据采集系统

## 项目概述

本项目旨在通过动态收集来自 PLC（可编程逻辑控制器）的数据，为用户提供实时监控和分析工业设备运行状态的能力。支持多种 PLC 类型、实时数据采集、消息队列、高效数据存储等功能，适用于工业自动化过程中的监控与控制、设备性能分析及故障诊断。

## 技术栈
- **编程语言**：C#
- **通信协议**：Modbus TCP/IP
- **数据库支持**：SQLite（支持 MySQL、PostgreSQL 可根据需要扩展）
- **消息队列**：RabbitMQ 或 Kafka（支持高并发数据采集）

## 主要功能

- **动态配置**：通过配置文件定义采集表、列名、频率，支持自定义数据点和采集方式。
- **多平台支持**：兼容 .NET Standard 2.0 和 .NET Standard 2.1。
- **多 PLC 数据采集**：支持从多个 PLC 周期性地采集实时数据。
- **模块化设计**：易于扩展和维护。
- **高效通讯**：基于 Modbus TCP 协议，实现稳定的高效通讯。
- **数据存储**：支持存储至本地 SQLite 数据库或云存储。
- **频率控制**：可配置采集频率，支持毫秒级控制。
- **错误处理**：支持断线重连与超时重试，确保系统稳定运行。
- **消息队列**：支持 RabbitMQ 或 Kafka，用于高并发数据采集。

## 安装与使用

### 1. 克隆仓库

```bash
git clone https://github.com/liuweichaox/DataAcquisition.git
```

### 2. 配置文件

在 `Configs` 文件夹中，每个表对应一个独立的 JSON 文件，您可以根据需要修改配置。配置文件定义了 PLC 信息、寄存器地址、数据类型等内容。示例配置文件如下：

**配置文件路径**：`Configs/`

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
        "EvalExpression":  null
      },
      {
        "ColumnName": "实时高度",
        "DataAddress": "D6100",
        "DataLength": 1,
        "DataType": "float",
        "EvalExpression":  null
      }
    ]
  }
}
```

### 3. 配置数据库与消息队列

- 配置您使用的数据库（例如 SQLite），确保能正常存储采集到的数据。
- 配置消息队列（RabbitMQ 或 Kafka），确保可以高效处理大量数据。

### 4. 启动数据采集服务

在 `Startup.cs` 中配置 `IDataAcquisitionService` 实例：

```csharp
builder.Services.AddSingleton<IDataAcquisitionService>(provider =>
{
    var hubContext = provider.GetService<IHubContext<DataHub>>();
    return new DataAcquisitionService(
        new DataAcquisitionConfigService(),
        (ipAddress, port) => new PlcClient(ipAddress, port),
        config => new SqLiteDataStorage(config),
        (factory, config) => new QueueManager(factory, config),
        (dataPoint, config) =>
        {
            dataPoint.Values["Timestamp"] = DateTime.Now;
        },
        async (message) =>
        {
            await hubContext.Clients.All.SendAsync("ReceiveMessage", message);
        });
});
```

### 5. 数据采集控制器

在 `DataAcquisitionController` 中定义 API 接口，控制数据采集任务的开始与停止：

```csharp
[Route("api/[controller]/[action]")]
public class DataAcquisitionController : ControllerBase
{
    private readonly IDataAcquisitionService _dataAcquisitionService;

    public DataAcquisitionController(IDataAcquisitionService dataAcquisitionService)
    {
        _dataAcquisitionService = dataAcquisitionService;
    }

    [HttpPost]
    public IActionResult StartCollectionTasks()
    {
        _dataAcquisitionService.StartCollectionTasks();
        return Ok("开始数据采集任务");
    }

    [HttpPost]
    public IActionResult StopCollectionTasks()
    {
        _dataAcquisitionService.StopCollectionTasks();
        return Ok("停止数据采集任务");
    }
}
```

## API 文档

### 开始数据采集任务

- **POST** `/api/DataAcquisition/StartCollectionTasks`
- **返回**：`开始数据采集任务`

### 停止数据采集任务

- **POST** `/api/DataAcquisition/StopCollectionTasks`
- **返回**：`停止数据采集任务`

## 贡献

如果您想为该项目贡献代码，欢迎提交 Pull Request！在提交之前，请确保代码通过了所有单元测试，并且没有引入任何破坏性变化。

## 许可

本项目使用 MIT 许可证，详情请参阅 [LICENSE](LICENSE) 文件。

---

感谢您使用 PLC 数据采集系统！如有问题，欢迎提出 issue 或进行讨论。

```