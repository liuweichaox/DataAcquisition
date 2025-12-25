# 📊 核心模块文档

本文档介绍 DataAcquisition 系统的核心模块设计和使用方法。

## PLC 客户端实现

系统支持多种 PLC 协议，每个协议都有对应的客户端实现：

| 协议         | 实现类                        | 描述                  |
| ------------ | ----------------------------- | --------------------- |
| Mitsubishi   | `MitsubishiPLCClientService`  | 三菱 PLC 通讯客户端   |
| Inovance     | `InovancePLCClientService`    | 汇川 PLC 通讯客户端   |
| Beckhoff ADS | `BeckhoffAdsPLCClientService` | 倍福 ADS 协议客户端   |
| Siemens      | `SiemensPLClientService`      | 西门子 PLC 通讯客户端 |

## ChannelCollector - 通道采集器

`ChannelCollector` 是系统的核心采集组件，负责从 PLC 读取数据。

### 核心方法

```csharp
public class ChannelCollector : IChannelCollector
{
    public async Task CollectAsync(DeviceConfig config, DataAcquisitionChannel channel,
        IPLCClientService client, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            // 检查 PLC 连接状态
            if (!await WaitForConnectionAsync(config, ct))
                continue;

            // 获取设备锁，确保线程安全的 PLC 访问
            if (!_plcLifecycle.TryGetLock(config.PLCCode, out var locker))
                continue;

            await locker.WaitAsync(ct);
            try
            {
                var timestamp = DateTime.Now;

                // 处理不同的采集模式
                if (channel.AcquisitionMode == AcquisitionMode.Always)
                {
                    await HandleUnconditionalCollectionAsync(config, channel, client, timestamp, ct);
                }
                else if (channel.AcquisitionMode == AcquisitionMode.Conditional)
                {
                    await HandleConditionalCollectionAsync(config, channel, client, timestamp, ct);
                }
            }
            finally
            {
                locker.Release();
            }
        }
    }
}
```

### 特性

- **线程安全**: 使用设备锁确保同一 PLC 设备的并发访问安全
- **连接管理**: 自动检测和处理 PLC 连接状态
- **多种采集模式**: 支持持续采集和条件触发采集
- **批量读取优化**: 支持批量读取多个寄存器，减少网络往返

## InfluxDbDataStorageService - 数据存储服务

`InfluxDbDataStorageService` 负责将采集的数据写入 InfluxDB 时序数据库。

### 核心方法

```csharp
public class InfluxDbDataStorageService : IDataStorageService
{
    public async Task<bool> SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return true;

        _writeStopwatch.Restart();
        var writeSuccess = false;
        Exception? writeException = null;
        var resetEvent = new System.Threading.ManualResetEventSlim(false);

        try
        {
            // 批量转换消息为数据点
            var points = dataMessages.Select(ConvertToPoint).ToList();
            using var writeApi = _client.GetWriteApi();

            // 设置错误处理回调，捕获写入失败
            writeApi.EventHandler += (sender, args) =>
            {
                writeException = new Exception($"InfluxDB 写入失败: {args.GetType().Name} - {args}");
                writeSuccess = false;
                resetEvent.Set();
                _logger.LogError(writeException, "[ERROR] InfluxDB 写入错误事件触发: {EventType} - {Message}",
                    args.GetType().Name, writeException.Message);
            };

            writeApi.WritePoints(_bucket, _org, points);
            writeApi.Flush();

            // 等待足够长的时间来检测错误（InfluxDB 异步写入，错误可能延迟）
            _logger.LogDebug("等待 InfluxDB 批量写入响应，最多等待 5 秒...");
            var errorOccurred = resetEvent.Wait(TimeSpan.FromSeconds(5));

            if (errorOccurred)
            {
                _logger.LogWarning("InfluxDB 批量写入错误事件已触发");
            }
            else
            {
                writeSuccess = true;
                _logger.LogDebug("InfluxDB 批量写入在 5 秒内未检测到错误，假设写入成功");
            }

            _writeStopwatch.Stop();

            if (!writeSuccess)
            {
                throw writeException ?? new Exception("InfluxDB 写入失败");
            }

            // 记录批量效率指标和写入延迟
            var batchSize = dataMessages.Count;
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            _metricsCollector?.RecordBatchWriteEfficiency(batchSize, _writeStopwatch.ElapsedMilliseconds);
            _metricsCollector?.RecordWriteLatency(measurement, _writeStopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            // 处理批量写入错误
            var plcCode = dataMessages.FirstOrDefault()?.PLCCode ?? "unknown";
            var measurement = dataMessages.FirstOrDefault()?.Measurement ?? "unknown";
            var channelCode = dataMessages.FirstOrDefault()?.ChannelCode;
            _metricsCollector?.RecordError(plcCode, measurement, channelCode);
            _logger.LogError(ex, "[ERROR] 时序数据库批量插入失败: {Message}", ex.Message);
            return false;
        }
        finally
        {
            resetEvent.Dispose();
        }
    }
}
```

### 特性

- **批量写入**: 支持批量写入，提高写入效率
- **错误处理**: 完善的错误处理和重试机制
- **性能监控**: 自动记录写入延迟和批量效率指标
- **异步处理**: 使用异步 API，提高性能

## MetricsCollector - 指标收集器

系统内置完整的监控指标，通过 Prometheus 格式暴露。

### 采集指标

- **`data_acquisition_collection_latency_ms`** - 采集延迟（从 PLC 读取到写入数据库的时间，毫秒）
- **`data_acquisition_collection_rate`** - 采集频率（每秒采集的数据点数，points/s）

### 队列指标

- **`data_acquisition_queue_depth`** - 队列深度（Channel 待读取 + 批量积累的待处理消息总数）
- **`data_acquisition_processing_latency_ms`** - 处理延迟（队列处理延迟，毫秒）

### 存储指标

- **`data_acquisition_write_latency_ms`** - 写入延迟（数据库写入延迟，毫秒）
- **`data_acquisition_batch_write_efficiency`** - 批量写入效率（批量大小/写入耗时，points/ms）

### 错误与连接指标

- **`data_acquisition_errors_total`** - 错误总数（按设备/通道统计）
- **`data_acquisition_connection_status_changes_total`** - 连接状态变化总数
- **`data_acquisition_connection_duration_seconds`** - 连接持续时间（秒）

## 接口抽象

系统采用接口抽象设计，主要接口包括：

- `IPLCClientService` - PLC 客户端服务接口
- `IChannelCollector` - 通道采集器接口
- `IDataStorageService` - 数据存储服务接口
- `IQueueService` - 队列服务接口
- `IMetricsCollector` - 指标收集器接口
- `IDeviceConfigService` - 设备配置服务接口

通过接口抽象，系统支持灵活的扩展和替换，可以轻松添加新的 PLC 协议、存储后端等。
