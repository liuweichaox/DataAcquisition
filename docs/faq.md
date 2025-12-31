# ❓ 常见问题 (FAQ)

本文档收集了 DataAcquisition 系统的常见问题和解答。

## 相关文档

- [快速开始指南](getting-started.md) - 从零开始使用系统
- [配置说明](configuration.md) - 详细的配置选项说明
- [API 使用示例](api-usage.md) - API 接口使用方法
- [性能优化建议](performance.md) - 优化系统性能
- [核心模块文档](modules.md) - 了解系统核心模块
- [数据处理流程](data-flow.md) - 理解数据流转过程
- [设计理念](design.md) - 了解系统设计思想

## Q: 数据丢失怎么办？

**A**: 系统采用 WAL-first 架构，所有数据先写入 Parquet 文件，再写入 InfluxDB。只有两者都成功才会删除 WAL 文件，确保数据零丢失。

如果发现数据丢失，可以：

1. 检查 `Data/parquet` 目录下是否有未处理的 WAL 文件
2. 查看日志确认写入失败原因
3. 系统会自动重试失败的写入操作

## Q: 如何添加新的 PLC 协议？

**A**: 需要修改源代码，实现 `IPLCClientService` 接口并在 `PLCClientFactory` 中注册。

**步骤**：

1. 创建新的 PLC 客户端类，实现 `IPLCClientService` 接口
2. 在 `PLCClientFactory` 中添加协议类型映射
3. 在 `PlcType` 枚举中添加新的协议类型
4. 在设备配置中使用新的协议类型

**注意**：这需要修改源代码并重新编译，建议有开发经验的用户进行。

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

**A**: 需要修改源代码，实现 `IDataStorageService` 接口并在 `Program.cs` 中注册。

**步骤**：

1. 创建新的存储服务类，实现 `IDataStorageService` 接口
2. 在 `Program.cs` 中注册新的存储服务
3. 系统会同时使用多个存储后端

**注意**：这需要修改源代码并重新编译，建议有开发经验的用户进行。

## Q: 如何调整采集频率？

**A**: 在设备配置文件中修改 `AcquisitionInterval` 参数（单位：毫秒）。

```json
{
  "Channels": [
    {
      "Measurement": "sensor",
      "ChannelCode": "CH01",
      "AcquisitionInterval": 100,
      "AcquisitionMode": "Always",
      "BatchSize": 10,
      "Metrics": [
        {
          "MetricName": "temperature",
          "FieldName": "temperature",
          "Register": "D6000",
          "Index": 0,
          "DataType": "short"
        }
      ]
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
      "Measurement": "production",
      "ChannelCode": "CH01",
      "EnableBatchRead": false,
      "BatchReadRegister": null,
      "BatchReadLength": 0,
      "BatchSize": 1,
      "AcquisitionInterval": 0,
      "AcquisitionMode": "Conditional",
      "Metrics": null,
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

**A**: 按以下步骤排查：

1. **检查 PLC 连接状态**:
   ```bash
   curl http://localhost:8001/api/DataAcquisition/plc-connections
   ```
   查看返回的连接状态信息

2. **检查网络连通性**:
   ```bash
   ping <PLC_IP地址>
   telnet <PLC_IP地址> <端口>
   ```
   确认 Edge Agent 能够访问 PLC 的 IP 和端口

3. **检查配置正确性**:
   - 确认设备配置文件中的 `Host`、`Port` 参数正确
   - 确认 `Type` 参数与实际的 PLC 类型匹配（Mitsubishi、Inovance、BeckhoffAds）
   - 确认 `PLCCode` 不为空且唯一

4. **查看日志信息**:
   ```bash
   curl "http://localhost:8001/api/logs?level=Error&page=1&pageSize=10"
   ```
   查看错误日志，定位具体问题

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
- BeckhoffAds（倍福）

其他协议可以通过实现 `IPLCClientService` 接口并在 `PLCClientFactory` 中注册来扩展支持。

## Q: 配置文件格式错误怎么办？

**A**: 配置文件必须是有效的 JSON 格式。常见错误：

1. **JSON 格式错误**: 检查是否有缺少逗号、引号未闭合等问题
2. **必填字段缺失**: 确保 `PLCCode`、`Host`、`Port`、`Type`、`Channels` 等必填字段存在
3. **字段类型错误**: 确保 `Port` 是数字，`IsEnabled` 是布尔值等

**验证方法**：
- 使用 JSON 验证工具（如在线 JSON 验证器）检查格式
- 查看日志中的配置加载错误信息
- 系统会在启动时验证配置，错误会记录在日志中

## Q: 采集任务没有启动怎么办？

**A**: 检查以下几点：

1. **设备是否启用**: 确认配置文件中 `IsEnabled` 为 `true`
2. **是否有采集通道**: 确认 `Channels` 数组不为空
3. **查看启动日志**: 检查日志中是否有 "启动采集任务失败" 的错误信息
4. **检查配置路径**: 确认配置文件在 `Configs/` 目录下，且文件名以 `.json` 结尾

**常见错误**：
- "设备编码为空"：检查 `PLCCode` 是否配置
- "没有配置采集通道"：检查 `Channels` 数组是否为空

## Q: 如何验证配置是否正确？

**A**: 可以通过以下方式验证：

1. **查看系统日志**: 启动 Edge Agent 后，查看日志中是否有配置加载错误
2. **检查连接状态**:
   ```bash
   curl http://localhost:8001/api/DataAcquisition/plc-connections
   ```
   如果配置正确，应该能看到设备连接状态

3. **检查指标数据**:
   ```bash
   curl http://localhost:8000/api/metrics-data
   ```
   如果开始采集，应该能看到采集相关的指标

4. **使用配置示例**: 参考项目中的 `TEST_PLC.json` 作为配置模板

## Q: 批量读取配置不正确会怎样？

**A**: 批量读取配置错误可能导致：

1. **数据读取错误**: 如果 `BatchReadLength` 设置过小，可能无法读取所有数据点
2. **索引错误**: 如果 `Metrics` 中的 `Index` 配置不正确，可能导致读取到错误的数据
3. **性能下降**: 如果应该使用批量读取但没有启用，会导致多次网络请求，性能下降

**配置建议**：
- 如果数据点连续，建议启用 `EnableBatchRead` 并正确配置 `BatchReadRegister` 和 `BatchReadLength`
- `Index` 应该与数据点在批量读取结果中的位置对应（注意数据类型占用的字节数）
- 如果不确定，可以先禁用批量读取，逐个读取寄存器进行测试

## Q: 如何查看系统是否正常运行？

**A**: 可以通过以下方式检查：

1. **检查服务状态**:
   ```bash
   # Central API
   curl http://localhost:8000/health

   # Edge Agent
   curl http://localhost:8001/api/DataAcquisition/plc-connections
   ```

2. **查看监控指标**:
   ```bash
   curl http://localhost:8000/api/metrics-data
   ```
   关注以下指标：
   - `data_acquisition_collection_rate`: 采集频率，应该大于 0
   - `data_acquisition_errors_total`: 错误总数，应该为 0 或很少
   - `data_acquisition_connection_duration_seconds`: 连接持续时间

3. **查看 Web 界面**: 访问 http://localhost:3000 查看可视化的系统状态

4. **检查日志**: 查看是否有错误日志，正常运行时应该主要是 Information 级别的日志

## 返回

- 返回 [README](../README.md) 查看项目概览和文档导航
