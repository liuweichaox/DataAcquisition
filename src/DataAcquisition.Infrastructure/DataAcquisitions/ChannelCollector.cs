using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCalc;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
///     通道采集器。根据配置从 Plc 读取数据，支持无条件和条件采集模式，将数据发布到队列。
/// </summary>
public class ChannelCollector : IChannelCollector
{
    private readonly int _connectionCheckRetryDelayMs;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ILogger<ChannelCollector> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly IPlcClientLifecycleService _plcLifecycle;
    private readonly IQueueService _queue;
    private readonly object _rateLock = new();
    private readonly IAcquisitionStateManager _stateManager;
    private readonly Stopwatch _stopwatch = new();
    private readonly int _triggerWaitDelayMs;
    private int _collectionCount;
    private DateTime _lastCollectionTime = DateTime.Now;

    /// <summary>
    ///     初始化通道采集器。
    /// </summary>
    public ChannelCollector(
        IHeartbeatMonitor heartbeatMonitor,
        IPlcClientLifecycleService plcLifecycle,
        ILogger<ChannelCollector> logger,
        IQueueService queue,
        IAcquisitionStateManager stateManager,
        IOptions<AcquisitionOptions> acquisitionOptions,
        IMetricsCollector? metricsCollector = null)
    {
        _heartbeatMonitor = heartbeatMonitor;
        _plcLifecycle = plcLifecycle;
        _logger = logger;
        _queue = queue;
        _stateManager = stateManager;
        _metricsCollector = metricsCollector;

        var options = acquisitionOptions.Value.ChannelCollector;
        _connectionCheckRetryDelayMs = options.ConnectionCheckRetryDelayMs;
        _triggerWaitDelayMs = options.TriggerWaitDelayMs;
    }

    /// <summary>
    ///     按通道配置执行采集任务。支持无条件采集（Always）和条件采集（Conditional）两种模式：
    ///     Always 模式：按配置频率无条件读取数据。
    ///     Conditional 模式：监控触发条件，触发后开启采集周期，结束时关闭周期。
    ///     数据处理：读取后执行表达式计算（若配置），然后异步发布到队列。
    /// </summary>
    /// <param name="config">设备配置，包含 Plc 连接信息和设备编码</param>
    /// <param name="dataAcquisitionChannel">采集通道配置，定义采集的测量值、数据点、触发条件等</param>
    /// <param name="client">Plc 通讯客户端，用于读取寄存器数据</param>
    /// <param name="ct">取消标记，用于取消采集任务</param>
    /// <exception cref="OperationCanceledException">当 ct 被取消时，会中断采集循环</exception>
    public async Task CollectAsync(DeviceConfig config, DataAcquisitionChannel dataAcquisitionChannel,
        IPlcClientService client, CancellationToken ct = default)
    {
        object? prevValue = null;
        while (!ct.IsCancellationRequested)
        {
            // 检查连接状态（快速检查，未连接时直接跳过，不延迟）
            if (!_heartbeatMonitor.TryGetConnectionHealth(config.PlcCode, out var isConnected) || !isConnected)
            {
                // 未连接时等待一小段时间再重试，避免CPU空转
                await Task.Delay(_connectionCheckRetryDelayMs, ct).ConfigureAwait(false);
                continue;
            }

            // 执行采集
            var timestamp = DateTime.Now;
            if (dataAcquisitionChannel.AcquisitionMode == AcquisitionMode.Always)
                await HandleUnconditionalCollectionAsync(config, dataAcquisitionChannel, client, timestamp, ct)
                    .ConfigureAwait(false);
            else if (dataAcquisitionChannel.AcquisitionMode == AcquisitionMode.Conditional)
                prevValue = await HandleConditionalCollectionAsync(config, dataAcquisitionChannel, client,
                    timestamp, prevValue, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     处理无条件采集。读取数据並按配置频率延迟。
    /// </summary>
    private async Task HandleUnconditionalCollectionAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPlcClientService client,
        DateTime timestamp,
        CancellationToken ct)
    {
        await HandleUnconditionalEventAsync(config.PlcCode, channel, client, timestamp).ConfigureAwait(false);
        // AcquisitionInterval = 0 表示最高频率采集（无延迟），> 0 表示延迟指定毫秒数
        if (channel.AcquisitionInterval > 0) await Task.Delay(channel.AcquisitionInterval, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     处理条件采集。监控触发條件，触发后会调用 HandleStartEventAsync 和 HandleEndEventAsync。
    /// </summary>
    private async Task<object?> HandleConditionalCollectionAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPlcClientService client,
        DateTime timestamp,
        object? prevValue,
        CancellationToken ct)
    {
        if (channel.ConditionalAcquisition == null) return prevValue;

        var conditionalAcq = channel.ConditionalAcquisition;
        if (string.IsNullOrWhiteSpace(conditionalAcq.Register) || string.IsNullOrWhiteSpace(conditionalAcq.DataType))
        {
            _logger.LogError("{PlcCode}-{ChannelCode}-{Measurement}:条件采集配置不完整，Register或DataType为空", config.PlcCode,
                channel.ChannelCode, channel.Measurement);
            await Task.Delay(_triggerWaitDelayMs, ct).ConfigureAwait(false);
            return prevValue;
        }

        // 读取触发寄存器的值
        var curr = await ReadPlcValueAsync(client, conditionalAcq.Register, conditionalAcq.DataType)
            .ConfigureAwait(false);

        // 评估触发条件
        var shouldStartTrigger = ShouldTrigger(conditionalAcq.StartTriggerMode, prevValue, curr);
        var shouldEndTrigger = ShouldTrigger(conditionalAcq.EndTriggerMode, prevValue, curr);

        // 优先处理结束事件（如果同时触发，先结束当前周期，再开始新周期）
        if (shouldEndTrigger) await HandleEndEventAsync(config.PlcCode, channel, timestamp).ConfigureAwait(false);

        if (shouldStartTrigger) await HandleStartTriggerAsync(config.PlcCode, channel, client, timestamp).ConfigureAwait(false);

        // 延迟并返回当前值用于下次比较
        await Task.Delay(_triggerWaitDelayMs, ct).ConfigureAwait(false);
        return curr;
    }

    /// <summary>
    ///     处理开始触发：记录指标并执行开始事件。
    /// </summary>
    private async Task HandleStartTriggerAsync(
        string plcCode,
        DataAcquisitionChannel channel,
        IPlcClientService client,
        DateTime timestamp)
    {
        _stopwatch.Restart();
        await HandleStartEventAsync(plcCode, channel, client, timestamp).ConfigureAwait(false);
        _stopwatch.Stop();

        RecordCollectionMetrics(plcCode, channel, _stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    ///     记录采集指标（延迟和频率）。
    /// </summary>
    private void RecordCollectionMetrics(string plcCode, DataAcquisitionChannel channel, long elapsedMilliseconds)
    {
        if (_metricsCollector == null) return;

        _metricsCollector.RecordCollectionLatency(plcCode, channel.ChannelCode, channel.Measurement,
            elapsedMilliseconds);

        lock (_rateLock)
        {
            _collectionCount++;
            var elapsed = (DateTime.Now - _lastCollectionTime).TotalSeconds;
            if (elapsed >= 1.0) // 每秒更新一次频率
            {
                var rate = _collectionCount / elapsed;
                _metricsCollector.RecordCollectionRate(plcCode, channel.ChannelCode, channel.Measurement, rate);
                _collectionCount = 0;
                _lastCollectionTime = DateTime.Now;
            }
        }
    }

    /// <summary>
    ///     对数据消息执行表达式计算。遍历数据值，对配置了 EvalExpression 的数据点使用 NCalc 计算表达式结果。
    ///     异常会被捕获并记录，不中断流程。此方法会修改 dataMessage 中的数据值。
    /// </summary>
    /// <param name="dataMessage">数据消息，包含要计算的数据值。计算结果会更新到此消息中。</param>
    /// <param name="metrics">指标配置列表，包含字段名和表达式配置。可以为 null，如果为 null 则不进行任何计算。</param>
    /// <remarks>
    ///     注意：此方法会修改 dataMessage 中的数据值。如果数据点配置了 EvalExpression，
    ///     计算结果会覆盖原始值。建议在调用此方法前保存原始数据（如果需要）。
    /// </remarks>
    private async Task EvaluateAsync(DataMessage dataMessage, List<Metric>? metrics)
    {
        await Task.Yield();
        try
        {
            if (metrics == null) return;

            foreach (var kv in dataMessage.DataValues.ToList())
            {
                var originalValue = kv.Value;
                if (!IsNumberType(originalValue)) continue;

                var metric = metrics.SingleOrDefault(x => x.FieldName == kv.Key);
                if (metric == null || originalValue is null) continue;

                var evalExpression = metric.EvalExpression;
                if (string.IsNullOrWhiteSpace(evalExpression)) continue;

                // originalValue 已经在上面的 null 检查中验证，使用 ! 断言非空
                var valueToEval = originalValue;

                var expression = new AsyncExpression(evalExpression)
                {
                    Parameters =
                    {
                        ["value"] = valueToEval
                    }
                };

                var evaluatedValue = await expression.EvaluateAsync().ConfigureAwait(false);
                dataMessage.UpdateDataValue(kv.Key, evaluatedValue ?? 0, originalValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling data point: {Message}", ex.Message);
        }
    }


    /// <summary>
    ///     处理无条件采集事件。生成 CycleId，读取数据，异步执行表达式计算和消息发布。
    /// </summary>
    /// <param name="plcCode">PLC编码</param>
    /// <param name="channel">采集通道配置</param>
    /// <param name="client">Plc 通讯客户端</param>
    /// <param name="timestamp">采集时间戳</param>
    private async Task HandleUnconditionalEventAsync(
        string plcCode,
        DataAcquisitionChannel channel,
        IPlcClientService client,
        DateTime timestamp)
    {
        try
        {
            var cycleId = Guid.NewGuid().ToString();
            var dataMessage = DataMessage.Create(cycleId, channel.Measurement, plcCode, channel.ChannelCode,
                EventType.Data, timestamp);

            // 读取指标数据
            await ReadMetricsAsync(client, channel, dataMessage).ConfigureAwait(false);

            // 异步处理表达式计算并发布消息，不阻塞采集循环
            _ = EvaluateAndPublishAsync(plcCode, channel, dataMessage);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(plcCode, channel.ChannelCode, channel.Measurement);
            _logger.LogError(ex, "{PlcCode}-{ChannelCode}-{Measurement}:采集异常: {Message}", plcCode, channel.ChannelCode,
                channel.Measurement, ex.Message);
        }
    }

    /// <summary>
    ///     处理条件采集的开始事件。使用 StateManager.StartCycle 生成不太 CycleId，然后读取数据并异步发布消息。
    /// </summary>
    /// <param name="plcCode">PLC编码</param>
    /// <param name="channel">采集通道配置</param>
    /// <param name="client">Plc 通讯客户端</param>
    /// <param name="timestamp">采集时间戳</param>
    private async Task HandleStartEventAsync(
        string plcCode,
        DataAcquisitionChannel channel,
        IPlcClientService client,
        DateTime timestamp)
    {
        try
        {
            var cycle = _stateManager.StartCycle(
                plcCode,
                channel.ChannelCode,
                channel.Measurement);
            var dataMessage = DataMessage.Create(cycle.CycleId, channel.Measurement, plcCode,
                channel.ChannelCode, EventType.Start, timestamp);

            // 读取指标数据
            await ReadMetricsAsync(client, channel, dataMessage).ConfigureAwait(false);

            // 异步处理表达式计算并发布消息，不阻塞采集循环
            _ = EvaluateAndPublishAsync(plcCode, channel, dataMessage);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(plcCode, channel.ChannelCode, channel.Measurement);
            _logger.LogError(ex, "{PlcCode}-{ChannelCode}-{Measurement}:采集异常: {Message}", plcCode, channel.ChannelCode,
                channel.Measurement, ex.Message);
        }
    }

    /// <summary>
    ///     处理结束事件：结束采集周期，写入End事件数据点。
    /// </summary>
    /// <returns>如果应该跳过后续处理则返回true，否则返回false</returns>
    /// <summary>
    ///     处理条件采集的结束事件：结束采集周期并发布 End 消息。
    ///     功能说明：
    ///     - 条件采集模式（<see cref="AcquisitionMode.Conditional" />）在触发 End 条件时调用此方法
    ///     - 结束当前活跃的采集周期，获取 CycleId 用于关联 Start 和 End 事件
    ///     - 创建 End 事件消息并发布到队列，标记采集周期的结束
    ///     处理流程：
    ///     1. 调用 StateManager.EndCycle 结束采集周期，获取 CycleId
    ///     2. 如果找不到对应的周期（异常情况），记录错误并返回 true（跳过后续处理）
    ///     3. 创建 DataMessage，事件类型为 End，包含 CycleId
    ///     4. 异步发布消息到队列（不阻塞采集循环）
    ///     异常情况处理：
    ///     - 找不到对应的采集周期（cycle == null）：
    ///     - 可能原因：Start 事件未正确触发、系统重启导致状态丢失、配置错误
    ///     - 处理方式：记录错误日志，返回 true 表示需要跳过后续处理
    ///     - 不创建 End 消息，避免数据不一致
    ///     数据一致性：
    ///     - End 事件必须与对应的 Start 事件关联（相同的 CycleId）
    ///     - 时序数据库不支持 Update 操作，因此 End 事件也是作为新的数据点写入
    ///     - 通过 EventType 标签区分 Start、End、Data 三种事件类型
    ///     异步处理：
    ///     - 消息发布使用 Task.Run 在后台线程执行
    ///     - 这样可以立即返回，让采集循环继续监控触发条件
    ///     返回值：
    ///     - true：表示需要跳过后续处理（通常是异常情况）
    ///     - false：表示正常处理完成（实际上方法返回 Task&lt;bool&gt;，异步执行）
    /// </summary>
    /// <param name="plcCode">PLC编码</param>
    /// <param name="channel">采集通道配置</param>
    /// <param name="timestamp">事件时间戳</param>
    /// <returns>
    ///     Task&lt;bool&gt;，true 表示需要跳过后续处理（异常情况），false 表示正常处理
    /// </returns>
    private async Task HandleEndEventAsync(string plcCode,
        DataAcquisitionChannel channel,
        DateTime timestamp)
    {
        try
        {
            // 结束采集周期，获取CycleId用于关联Start事件
            var cycle = _stateManager.EndCycle(plcCode, channel.ChannelCode, channel.Measurement);
            if (cycle == null)
            {
                // 异常情况：找不到对应的cycle，记录警告并跳过
                _logger.LogError(
                    "{PlcCode}-{ChannelCode}-{Measurement} End事件触发但找不到对应的采集周期，可能Start事件未正确触发或系统重启导致状态丢失",
                    plcCode, channel.ChannelCode, channel.Measurement);
                return;
            }

            // 创建End事件数据点（时序数据库不支持Update，改为写入新数据点）
            var dataMessage = DataMessage.Create(cycle.CycleId, channel.Measurement, plcCode,
                channel.ChannelCode, EventType.End, timestamp);
            await EvaluateAndPublishAsync(plcCode, channel, dataMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PlcCode}-{ChannelCode}-{Measurement}:采集异常: {Message}", plcCode, channel.ChannelCode,
                channel.Measurement, ex.Message);
        }
    }

    /// <summary>
    ///     从 Plc 读取数据点的值并添加到数据消息中。支持批量读取（地址连续）和单点读取（地址分散）两种方式。
    /// </summary>
    private async Task ReadMetricsAsync(
        IPlcClientService client,
        DataAcquisitionChannel channel,
        DataMessage dataMessage)
    {
        if (channel.Metrics == null) return;

        if (channel.EnableBatchRead)
        {
            var batchData = await client.ReadAsync(channel.BatchReadRegister, channel.BatchReadLength)
                .ConfigureAwait(false);
            var buffer = batchData.Content;
            foreach (var metric in channel.Metrics)
            {
                var value = TransValue(client, buffer, metric.Index, metric.StringByteLength, metric.DataType,
                    metric.Encoding);
                dataMessage.AddDataValue(metric.FieldName, value);
            }
        }
        else
        {
            foreach (var metric in channel.Metrics)
            {
                var value = await ReadPlcValueAsync(
                    client,
                    metric.Register,
                    metric.DataType,
                    metric.StringByteLength,
                    metric.Encoding).ConfigureAwait(false);
                dataMessage.AddDataValue(metric.FieldName, value);
            }
        }
    }

    /// <summary>
    ///     异步处理数据消息（表达式计算和发布），不阻塞采集循环。
    /// </summary>
    private async Task EvaluateAndPublishAsync(string plcCode, DataAcquisitionChannel channel,
        DataMessage dataMessage)
    {
        try
        {
            await EvaluateAsync(dataMessage, channel.Metrics).ConfigureAwait(false);
            await _queue.PublishAsync(dataMessage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(plcCode, channel.ChannelCode, channel.Measurement);
            _logger.LogError(ex, "{PlcCode}-{ChannelCode}-{Measurement}:异步处理数据消息失败: {Message}", plcCode,
                channel.ChannelCode, channel.Measurement, ex.Message);
        }
    }

    /// <summary>
    ///     判断对象是否为数值类型。
    /// </summary>
    private static bool IsNumberType(object? value)
    {
        return value is ushort or uint or ulong or short or int or long or float or double;
    }

    /// <summary>
    ///     读取指定寄存器的值。
    /// </summary>
    private static async Task<object> ReadPlcValueAsync(
        IPlcClientService client,
        string register,
        string dataType,
        int stringLength = 0,
        string? encoding = null)
    {
        return dataType.ToLower() switch
        {
            "ushort" => await client.ReadUShortAsync(register).ConfigureAwait(false),
            "uint" => await client.ReadUIntAsync(register).ConfigureAwait(false),
            "ulong" => await client.ReadULongAsync(register).ConfigureAwait(false),
            "short" => await client.ReadShortAsync(register).ConfigureAwait(false),
            "int" => await client.ReadIntAsync(register).ConfigureAwait(false),
            "long" => await client.ReadLongAsync(register).ConfigureAwait(false),
            "float" => await client.ReadFloatAsync(register).ConfigureAwait(false),
            "double" => await client.ReadDoubleAsync(register).ConfigureAwait(false),
            "string" => await client
                .ReadStringAsync(register, (ushort)stringLength, Encoding.GetEncoding(encoding ?? "UTF8"))
                .ConfigureAwait(false),
            "bool" => await client.ReadBoolAsync(register).ConfigureAwait(false),
            _ => throw new NotSupportedException($"不支持的数据类型: {dataType}")
        };
    }

    /// <summary>
    ///     按数据类型转换缓冲区中的值。
    /// </summary>
    private static dynamic? TransValue(IPlcClientService client, byte[] buffer, int index, int length, string dataType,
        string encoding)
    {
        return dataType.ToLower() switch
        {
            "ushort" => client.TransUShort(buffer, index),
            "uint" => client.TransUInt(buffer, index),
            "ulong" => client.TransULong(buffer, index),
            "short" => client.TransShort(buffer, index),
            "int" => client.TransInt(buffer, index),
            "long" => client.TransLong(buffer, index),
            "float" => client.TransFloat(buffer, index),
            "double" => client.TransDouble(buffer, index),
            "string" => client.TransString(buffer, index, length, Encoding.GetEncoding(encoding)),
            "bool" => client.TransBool(buffer, index),
            _ => null
        };
    }

    /// <summary>
    ///     判断是否应该触发采集。RisingEdge：从0变非0时触发开始；FallingEdge：从非0变0时触发结束。
    ///     如果 mode 为 null 返回 false；首次读取（previousValue 或 currentValue 为 null）返回 true。
    /// </summary>
    private static bool ShouldTrigger(AcquisitionTrigger? mode, object? previousValue, object? currentValue)
    {
        // 如果 mode 为 null，不触发
        if (!mode.HasValue) return false;

        // 如果前一个值或当前值为null，默认触发（首次读取）
        if (previousValue == null || currentValue == null) return true;

        var prev = Convert.ToDecimal(previousValue);
        var curr = Convert.ToDecimal(currentValue);

        return mode.Value switch
        {
            AcquisitionTrigger.RisingEdge => prev < curr,
            AcquisitionTrigger.FallingEdge => prev > curr,
            _ => false
        };
    }
}