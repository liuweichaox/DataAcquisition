# ❓ 常见问题 (FAQ)

本文档收集了 DataAcquisition 系统的常见问题和解答。

## Q: 数据丢失怎么办？

**A**: 系统采用 WAL-first 架构，所有数据先写入 Parquet 文件，再写入 InfluxDB。只有两者都成功才会删除 WAL 文件，确保数据零丢失。

如果发现数据丢失，可以：

1. 检查 `Data/parquet` 目录下是否有未处理的 WAL 文件
2. 查看日志确认写入失败原因
3. 系统会自动重试失败的写入操作

## Q: 如何添加新的 PLC 协议？

**A**: 实现 `IPLCClientService` 接口，并在 `PLCClientFactory` 中注册新的协议支持。

步骤：

1. 创建新的 PLC 客户端类，实现 `IPLCClientService` 接口
2. 在 `PLCClientFactory` 中添加协议类型映射
3. 在设备配置中使用新的协议类型

示例代码：

```csharp
public class CustomPLCClientService : IPLCClientService
{
    // 实现接口方法
}

// 在 PLCClientFactory 中注册
_factories[PlcType.Custom] = () => new CustomPLCClientService();
```

## Q: 配置修改后需要重启吗？

**A**: 不需要。系统使用 FileSystemWatcher 监控配置文件变化，支持热更新。

配置文件修改后，系统会自动：

1. 检测配置文件变化
2. 验证配置格式
3. 重新加载配置
4. 应用新配置（无需重启服务）

## Q: 监控指标在哪里查看？

**A**: 访问 http://localhost:8000/metrics 查看可视化界面或获取 Prometheus 原始格式指标，或 http://localhost:8000/api/metrics-data 获取 JSON 格式指标数据（推荐）。

### Prometheus 格式

```bash
curl http://localhost:8000/metrics
```

### JSON 格式

```bash
curl http://localhost:8000/api/metrics-data
```

### Web 界面

访问 Central Web 界面（http://localhost:3000）查看可视化的监控指标。

## Q: 如何扩展存储后端？

**A**: 实现 `IDataStorageService` 接口，保持与队列服务的写入契约一致性。

步骤：

1. 创建新的存储服务类，实现 `IDataStorageService` 接口
2. 在 `Program.cs` 中注册新的存储服务
3. 系统会同时使用多个存储后端

示例代码：

```csharp
public class CustomStorageService : IDataStorageService
{
    public async Task<bool> SaveBatchAsync(List<DataMessage> dataMessages)
    {
        // 实现存储逻辑
    }
}
```

## Q: 如何调整采集频率？

**A**: 在设备配置文件中修改 `AcquisitionInterval` 参数（单位：毫秒）。

```json
{
  "Channels": [
    {
      "AcquisitionInterval": 100,  // 100ms 采集一次
      // ...
    }
  ]
}
```

## Q: 条件采集如何配置？

**A**: 在通道配置中设置 `AcquisitionMode` 为 `Conditional`，并配置 `ConditionalAcquisition` 对象。

```json
{
  "Channels": [
    {
      "AcquisitionMode": "Conditional",
      "ConditionalAcquisition": {
        "Register": "D210",
        "DataType": "short",
        "StartTriggerMode": "RisingEdge",
        "EndTriggerMode": "FallingEdge"
      }
    }
  ]
}
```

## Q: 如何排查连接问题？

**A**: 检查以下几点：

1. **PLC 连接状态**: 访问 `/api/DataAcquisition/GetPLCConnectionStatus` 查看连接状态
2. **网络连通性**: 确认 Edge Agent 能够访问 PLC 的 IP 和端口
3. **配置正确性**: 检查设备配置文件中的 Host、Port 等参数
4. **日志信息**: 查看日志中的错误信息，定位具体问题

## Q: WAL 文件过多怎么办？

**A**: WAL 文件过多通常表示 InfluxDB 写入失败。解决方案：

1. 检查 InfluxDB 连接和配置
2. 查看日志确认写入失败原因
3. 修复问题后，系统会自动处理积压的 WAL 文件
4. 如需手动清理，先确认数据已写入 InfluxDB

## Q: 如何部署到生产环境？

**A**: 建议步骤：

1. **配置生产参数**: 修改 `appsettings.json` 中的配置
2. **设置环境变量**: 使用环境变量管理敏感信息（如 Token）
3. **配置日志级别**: 生产环境建议使用 Warning 级别
4. **启用监控**: 配置 Prometheus 监控和告警
5. **备份策略**: 配置 WAL 文件和数据库的备份策略

## Q: 支持哪些 PLC 协议？

**A**: 目前支持以下 PLC 协议：

- Mitsubishi（三菱）
- Inovance（汇川）
- Beckhoff ADS（倍福）
- Siemens（西门子）
- Modbus TCP

其他协议可以通过实现 `IPLCClientService` 接口扩展支持。
