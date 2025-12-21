using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NCalc;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
///     通道采集器，根据配置从 PLC 读取数据并发布。
///     职责：
///     - 监控PLC寄存器状态，判断触发条件
///     - 执行数据采集（批量读取或单点读取）
///     - 管理采集周期状态（通过IAcquisitionStateManager）
///     - 发布数据消息到队列
/// </summary>
public class ChannelCollector : IChannelCollector
{
    private readonly int _connectionCheckRetryDelayMs;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ILogger<ChannelCollector> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly IPLCClientLifecycleService _plcLifecycle;
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
        IPLCClientLifecycleService plcLifecycle,
        ILogger<ChannelCollector> logger,
        IQueueService queue,
        IAcquisitionStateManager stateManager,
        IConfiguration configuration,
        IMetricsCollector? metricsCollector = null)
    {
        _heartbeatMonitor = heartbeatMonitor;
        _plcLifecycle = plcLifecycle;
        _logger = logger;
        _queue = queue;
        _stateManager = stateManager;
        _metricsCollector = metricsCollector;

        var options = new ChannelCollectorOptions
        {
            ConnectionCheckRetryDelayMs =
                int.TryParse(configuration["Acquisition:ChannelCollector:ConnectionCheckRetryDelayMs"],
                    out var retryDelay)
                    ? retryDelay
                    : 100,
            TriggerWaitDelayMs =
                int.TryParse(configuration["Acquisition:ChannelCollector:TriggerWaitDelayMs"], out var waitDelay)
                    ? waitDelay
                    : 100
        };
        _connectionCheckRetryDelayMs = options.ConnectionCheckRetryDelayMs;
        _triggerWaitDelayMs = options.TriggerWaitDelayMs;
    }

    /// <summary>
    ///     按通道配置执行采集任务。
    ///     工作流程：
    ///     1. 连接检查：循环检查 PLC 连接状态，如果未连接则等待并重试
    ///     2. 获取锁：确保同一设备的多个通道不会并发访问 PLC（线程安全）
    ///     3. 根据采集模式执行：
    ///     - <see cref="AcquisitionMode.Always" />: 无条件采集，按配置的频率持续采集数据
    ///     - <see cref="AcquisitionMode.Conditional" />: 条件采集，根据触发条件决定是否采集
    ///     4. 数据读取：根据配置选择批量读取或单点读取
    ///     5. 表达式计算：对数值类型的数据点执行表达式计算（如果配置了 EvalExpression）
    ///     6. 发布消息：将处理后的数据发布到队列，等待后续存储处理
    ///     无条件采集模式（Always）：
    ///     - 每次循环都读取数据并发布
    ///     - 根据 AcquisitionInterval 配置延迟（0 表示最高频率，无延迟）
    ///     条件采集模式（Conditional）：
    ///     - 持续监控触发寄存器（ConditionalAcquisition.Register）
    ///     - 根据 StartTriggerMode 和 EndTriggerMode 判断是否触发 Start/End 事件
    ///     - Start 事件：创建新的采集周期（CycleId），开始持续采集数据
    ///     - End 事件：结束当前采集周期，停止采集
    ///     - 触发检查延迟：根据 TriggerWaitDelayMs 配置延迟（默认 100ms），避免 CPU 空转
    ///     - 优先处理 End 事件：如果同时触发 Start 和 End，先处理 End（结束当前周期），再处理 Start（开始新周期）
    ///     异常处理：
    ///     - 连接异常：记录错误并继续循环，等待下次连接检查
    ///     - 读取异常：记录错误，不发布数据，继续下一次采集
    ///     - 表达式计算异常：记录错误，使用原始值，不中断采集流程
    ///     性能优化：
    ///     - 表达式计算和消息发布使用 Task.Run 异步执行，不阻塞采集循环
    ///     - 批量读取模式可以减少 PLC 通信次数，提高采集效率
    ///     - 使用 SemaphoreSlim 锁确保同一设备的通道不会并发访问，避免 PLC 通信冲突
    /// </summary>
    /// <param name="config">设备配置，包含 PLC 连接信息和设备编码</param>
    /// <param name="dataAcquisitionChannel">采集通道配置，定义采集的测量值、数据点、触发条件等</param>
    /// <param name="client">PLC 通讯客户端，用于读取寄存器数据</param>
    /// <param name="ct">取消标记，用于取消采集任务</param>
    /// <exception cref="OperationCanceledException">当 ct 被取消时，会中断采集循环</exception>
    public async Task CollectAsync(DeviceConfig config, DataAcquisitionChannel dataAcquisitionChannel,
        IPLCClientService client, CancellationToken ct = default)
    {
        await Task.Yield();
        object? prevValue = null;
        while (!ct.IsCancellationRequested)
        {
            // 检查连接状态
            if (!await WaitForConnectionAsync(config, ct).ConfigureAwait(false)) continue;

            // 获取锁并执行采集
            if (!_plcLifecycle.TryGetLock(config.PLCCode, out var locker))
            {
                _logger.LogError("{PLCCode}-未找到锁对象，跳过本次采集", config.PLCCode);
                await Task.Delay(_connectionCheckRetryDelayMs, ct).ConfigureAwait(false);
                continue;
            }

            await locker.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var timestamp = DateTime.Now;
                if (dataAcquisitionChannel.AcquisitionMode == AcquisitionMode.Always)
                    await HandleUnconditionalCollectionAsync(config, dataAcquisitionChannel, client, timestamp, ct)
                        .ConfigureAwait(false);
                else if (dataAcquisitionChannel.AcquisitionMode == AcquisitionMode.Conditional)
                    prevValue = await HandleConditionalCollectionAsync(config, dataAcquisitionChannel, client,
                        timestamp, prevValue, ct).ConfigureAwait(false);
            }
            finally
            {
                locker.Release();
            }
        }
    }

    /// <summary>
    ///     等待 PLC 连接就绪。
    /// </summary>
    /// <returns>如果连接就绪返回 true，否则返回 false 并已延迟等待</returns>
    private async Task<bool> WaitForConnectionAsync(DeviceConfig config, CancellationToken ct)
    {
        if (_heartbeatMonitor.TryGetConnectionHealth(config.PLCCode, out var isConnected) && isConnected) return true;

        await Task.Delay(_connectionCheckRetryDelayMs, ct).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    ///     处理无条件采集。
    /// </summary>
    private async Task HandleUnconditionalCollectionAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPLCClientService client,
        DateTime timestamp,
        CancellationToken ct)
    {
        await HandleUnconditionalEventAsync(config, channel, client, timestamp).ConfigureAwait(false);
        // AcquisitionInterval = 0 表示最高频率采集（无延迟），> 0 表示延迟指定毫秒数
        if (channel.AcquisitionInterval > 0) await Task.Delay(channel.AcquisitionInterval, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     处理条件采集。
    /// </summary>
    /// <returns>更新后的 prevValue，用于下次循环比较</returns>
    private async Task<object?> HandleConditionalCollectionAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPLCClientService client,
        DateTime timestamp,
        object? prevValue,
        CancellationToken ct)
    {
        if (channel.ConditionalAcquisition == null) return prevValue;

        var conditionalAcq = channel.ConditionalAcquisition;
        if (string.IsNullOrWhiteSpace(conditionalAcq.Register) || string.IsNullOrWhiteSpace(conditionalAcq.DataType))
        {
            _logger.LogError("{PLCCode}-{Measurement}:条件采集配置不完整，Register或DataType为空", config.PLCCode,
                channel.Measurement);
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
        if (shouldEndTrigger) await HandleEndEventAsync(config, channel, timestamp).ConfigureAwait(false);

        if (shouldStartTrigger) await HandleStartTriggerAsync(config, channel, client, timestamp).ConfigureAwait(false);

        // 延迟并返回当前值用于下次比较
        await Task.Delay(_triggerWaitDelayMs, ct).ConfigureAwait(false);
        return curr;
    }

    /// <summary>
    ///     处理开始触发：记录指标并执行开始事件。
    /// </summary>
    private async Task HandleStartTriggerAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPLCClientService client,
        DateTime timestamp)
    {
        _stopwatch.Restart();
        await HandleStartEventAsync(config, channel, client, timestamp).ConfigureAwait(false);
        _stopwatch.Stop();

        RecordCollectionMetrics(config, channel, _stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    ///     记录采集指标（延迟和频率）。
    /// </summary>
    private void RecordCollectionMetrics(DeviceConfig config, DataAcquisitionChannel channel, long elapsedMilliseconds)
    {
        if (_metricsCollector == null) return;

        _metricsCollector.RecordCollectionLatency(config.PLCCode, channel.Measurement, elapsedMilliseconds,
            channel.ChannelCode);

        lock (_rateLock)
        {
            _collectionCount++;
            var elapsed = (DateTime.Now - _lastCollectionTime).TotalSeconds;
            if (elapsed >= 1.0) // 每秒更新一次频率
            {
                var rate = _collectionCount / elapsed;
                _metricsCollector.RecordCollectionRate(config.PLCCode, channel.Measurement, rate, channel.ChannelCode);
                _collectionCount = 0;
                _lastCollectionTime = DateTime.Now;
            }
        }
    }

    /// <summary>
    ///     对数据消息进行表达式计算并更新数据值。
    ///     功能说明：
    ///     - 遍历数据消息中的所有数据值，对数值类型的数据点执行表达式计算
    ///     - 如果数据点配置了 EvalExpression，使用 NCalc 库计算表达式结果
    ///     - 表达式可以使用变量 "value" 引用原始数据值，例如："value * 0.1"、"value + 273.15"
    ///     - 计算结果会覆盖原始数据值（AddDataValue 方法如果 key 已存在会更新值）
    ///     处理流程：
    ///     1. 检查数据点列表是否为 null，如果为 null 则直接返回
    ///     2. 遍历数据消息中的所有数据值（DataValues）
    ///     3. 只处理数值类型的数据（ushort, uint, ulong, short, int, long, float, double）
    ///     4. 查找对应的数据点配置（通过 FieldName 匹配）
    ///     5. 如果数据点配置了 EvalExpression，创建 AsyncExpression 并计算
    ///     6. 将计算结果更新到数据消息中（如果计算结果为 null，使用 0 作为默认值）
    ///     表达式语法：
    ///     - 支持标准的数学运算符：+, -, *, /, %, ^
    ///     - 支持数学函数：sin, cos, tan, log, exp, sqrt, abs 等
    ///     - 支持逻辑运算符：and, or, not
    ///     - 支持比较运算符：==, !=, &lt;, &gt;, &lt;=, &gt;=
    ///     - 变量 "value" 代表原始数据值
    ///     示例表达式：
    ///     - "value * 0.1"：将原始值乘以 0.1（例如：温度转换）
    ///     - "value + 273.15"：将原始值加上 273.15（例如：摄氏度转开尔文）
    ///     - "value / 1000"：将原始值除以 1000（例如：单位转换）
    ///     - "value &gt; 100 ? 1 : 0"：如果值大于 100 返回 1，否则返回 0（条件表达式）
    ///     异常处理：
    ///     - 表达式计算异常会被捕获并记录到事件日志
    ///     - 异常不会中断流程，其他数据点的计算会继续执行
    ///     - 如果表达式计算失败，原始值不会被修改
    ///     性能考虑：
    ///     - 此方法使用 Task.Yield 让出控制权，避免阻塞调用线程
    ///     - 表达式计算是同步操作，但通过异步方法调用，可以更好地利用线程池
    /// </summary>
    /// <param name="dataMessage">数据消息，包含要计算的数据值。计算结果会更新到此消息中。</param>
    /// <param name="dataPoints">数据点配置列表，包含字段名和表达式配置。可以为 null，如果为 null 则不进行任何计算。</param>
    /// <remarks>
    ///     注意：此方法会修改 dataMessage 中的数据值。如果数据点配置了 EvalExpression，
    ///     计算结果会覆盖原始值。建议在调用此方法前保存原始数据（如果需要）。
    /// </remarks>
    private async Task EvaluateAsync(DataMessage dataMessage, List<DataPoint>? dataPoints)
    {
        await Task.Yield();
        try
        {
            if (dataPoints == null) return;

            foreach (var kv in dataMessage.DataValues.ToList())
            {
                var originalValue = kv.Value;
                if (!IsNumberType(originalValue)) continue;

                var register = dataPoints.SingleOrDefault(x => x.FieldName == kv.Key);
                if (register == null || originalValue is null) continue;

                var evalExpression = register.EvalExpression;
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
                dataMessage.AddDataValue(kv.Key, evaluatedValue ?? 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling data point: {Message}", ex.Message);
        }
    }


    /// <summary>
    ///     处理无条件采集事件：读取数据并发布消息。
    ///     功能说明：
    ///     - 无条件采集模式（<see cref="AcquisitionMode.Always" />）使用此方法处理每次采集
    ///     - 每次调用都会生成新的 CycleId（GUID），每个数据点都是独立的事件
    ///     - 读取所有配置的数据点值，执行表达式计算（如果配置了），然后发布到队列
    ///     处理流程：
    ///     1. 生成新的 CycleId（每次都是新的，表示独立的数据点）
    ///     2. 创建 DataMessage，事件类型为 Data
    ///     3. 从 PLC 读取所有数据点的值（ReadDataPointsAsync）
    ///     4. 异步执行表达式计算和消息发布（不阻塞采集循环）
    ///     异步处理：
    ///     - 表达式计算和消息发布使用 Task.Run 在后台线程执行
    ///     - 这样可以立即返回，让采集循环继续下一次采集，提高吞吐量
    ///     - 如果异步处理失败，会记录错误但不影响采集循环
    ///     异常处理：
    ///     - 读取异常：记录错误，不发布消息，采集循环继续
    ///     - 表达式计算异常：在异步任务中记录错误，不影响采集循环
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="channel">采集通道配置</param>
    /// <param name="client">PLC 通讯客户端</param>
    /// <param name="timestamp">采集时间戳</param>
    private async Task HandleUnconditionalEventAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPLCClientService client,
        DateTime timestamp)
    {
        try
        {
            var cycleId = Guid.NewGuid().ToString();
            var dataMessage = DataMessage.Create(cycleId, channel.Measurement, config.PLCCode, channel.ChannelCode,
                EventType.Data, timestamp);

            // 读取数据点
            await ReadDataPointsAsync(client, channel, dataMessage).ConfigureAwait(false);

            // 异步处理表达式计算并发布消息，不阻塞采集循环
            _ = ProcessAndPublishMessageAsync(config, channel, dataMessage);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(config.PLCCode, channel.Measurement);
            _logger.LogError(ex, "{PLCCode}-{Measurement}:采集异常: {Message}", config.PLCCode, channel.Measurement,
                ex.Message);
        }
    }

    /// <summary>
    ///     处理条件采集的开始事件：生成采集周期，读取数据并发布消息。
    ///     功能说明：
    ///     - 条件采集模式（<see cref="AcquisitionMode.Conditional" />）在触发 Start 条件时调用此方法
    ///     - 创建一个新的采集周期（CycleId），后续的数据点都会关联到这个周期
    ///     - Start 事件会标记一个采集周期的开始，直到对应的 End 事件结束
    ///     处理流程：
    ///     1. 调用 StateManager.StartCycle 创建新的采集周期，生成唯一的 CycleId
    ///     2. 创建 DataMessage，事件类型为 Start，包含 CycleId
    ///     3. 从 PLC 读取所有数据点的值（ReadDataPointsAsync）
    ///     4. 异步执行表达式计算和消息发布（不阻塞采集循环）
    ///     采集周期管理：
    ///     - 使用复合键（plcCode:measurement）存储周期状态
    ///     - 如果已存在活跃周期，会先移除旧的周期（处理异常情况）
    ///     - 同一设备的多个测量值可以同时进行条件采集（独立周期）
    ///     异步处理：
    ///     - 表达式计算和消息发布使用 Task.Run 在后台线程执行
    ///     - 这样可以立即返回，让采集循环继续监控触发条件
    ///     异常处理：
    ///     - 读取异常：记录错误，不发布消息，周期状态可能不一致
    ///     - 表达式计算异常：在异步任务中记录错误
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="channel">采集通道配置</param>
    /// <param name="client">PLC 通讯客户端</param>
    /// <param name="timestamp">采集时间戳</param>
    private async Task HandleStartEventAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPLCClientService client,
        DateTime timestamp)
    {
        try
        {
            var cycle = _stateManager.StartCycle(
                config.PLCCode,
                channel.Measurement,
                channel.ChannelCode);
            var dataMessage = DataMessage.Create(cycle.CycleId, channel.Measurement, config.PLCCode,
                channel.ChannelCode, EventType.Start, timestamp);

            // 读取数据点
            await ReadDataPointsAsync(client, channel, dataMessage).ConfigureAwait(false);

            // 异步处理表达式计算并发布消息，不阻塞采集循环
            _ = ProcessAndPublishMessageAsync(config, channel, dataMessage);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(config.PLCCode, channel.Measurement);
            _logger.LogError(ex, "{PLCCode}-{Measurement}:采集异常: {Message}", config.PLCCode, channel.Measurement,
                ex.Message);
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
    /// <param name="config">设备配置</param>
    /// <param name="channel">采集通道配置</param>
    /// <param name="timestamp">事件时间戳</param>
    /// <returns>
    ///     Task&lt;bool&gt;，true 表示需要跳过后续处理（异常情况），false 表示正常处理
    /// </returns>
    private async Task HandleEndEventAsync(DeviceConfig config,
        DataAcquisitionChannel channel,
        DateTime timestamp)
    {
        try
        {
            // 结束采集周期，获取CycleId用于关联Start事件
            var cycle = _stateManager.EndCycle(config.PLCCode, channel.Measurement);
            if (cycle == null)
            {
                // 异常情况：找不到对应的cycle，记录警告并跳过
                _logger.LogError(
                    "{PLCCode}-{Measurement} End事件触发但找不到对应的采集周期，可能Start事件未正确触发或系统重启导致状态丢失",
                    config.PLCCode, channel.Measurement);
                return;
            }

            // 创建End事件数据点（时序数据库不支持Update，改为写入新数据点）
            var dataMessage = DataMessage.Create(cycle.CycleId, channel.Measurement, config.PLCCode,
                channel.ChannelCode, EventType.End, timestamp);
            await PublishEndEventMessageAsync(config, channel, dataMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PLCCode}-{Measurement}:采集异常: {Message}", config.PLCCode, channel.Measurement,
                ex.Message);
        }
    }

    /// <summary>
    ///     读取数据点：支持批量读取和单点读取两种方式。
    /// </summary>
    /// <summary>
    ///     从 PLC 读取数据点的值并添加到数据消息中。
    ///     读取模式：
    ///     - 批量读取模式（EnableBatchRead = true）：
    ///     - 使用 ReadAsync 一次性读取连续寄存器区域（从 BatchReadRegister 开始，长度为 BatchReadLength）
    ///     - 从读取的缓冲区中按索引（Index）提取各个数据点的值
    ///     - 适用于数据点地址连续的情况，可以减少 PLC 通信次数，提高采集效率
    ///     - 使用 TransValue 方法从缓冲区字节数组中转换数据
    ///     - 单点读取模式（EnableBatchRead = false）：
    ///     - 对每个数据点单独调用 ReadPlcValueAsync 读取寄存器值
    ///     - 适用于数据点地址不连续的情况，或者数据点数量较少的情况
    ///     - 每次读取都需要一次 PLC 通信，通信次数 = 数据点数量
    ///     数据点配置：
    ///     - 每个数据点必须配置 Register（寄存器地址）和 DataType（数据类型）
    ///     - 如果使用批量读取，还需要配置 Index（在缓冲区中的索引位置）
    ///     - 字符串类型需要配置 StringByteLength（字节长度）和 Encoding（编码格式）
    ///     数据类型支持：
    ///     - 数值类型：ushort, uint, ulong, short, int, long, float, double
    ///     - 布尔类型：bool
    ///     - 字符串类型：string（需要指定字节长度和编码）
    ///     错误处理：
    ///     - 如果数据点列表为 null，直接返回，不进行任何读取
    ///     - 读取失败会抛出异常，由调用方处理
    ///     性能优化建议：
    ///     - 如果数据点地址连续，建议使用批量读取模式以提高效率
    ///     - 批量读取模式可以减少网络往返次数，特别适合高频采集场景
    ///     - 单点读取模式适合地址分散的数据点，但会增加通信开销
    /// </summary>
    /// <param name="client">PLC 通讯客户端，用于读取寄存器数据</param>
    /// <param name="channel">采集通道配置，包含数据点列表和读取模式配置</param>
    /// <param name="dataMessage">数据消息，读取的数据值会添加到 DataValues 字典中，使用 FieldName 作为 key</param>
    /// <exception cref="NotSupportedException">当数据类型不支持时抛出</exception>
    /// <exception cref="Exception">PLC 读取操作可能抛出各种异常（网络异常、地址错误等），由调用方处理</exception>
    private async Task ReadDataPointsAsync(
        IPLCClientService client,
        DataAcquisitionChannel channel,
        DataMessage dataMessage)
    {
        if (channel.DataPoints == null) return;

        if (channel.EnableBatchRead)
        {
            var batchData = await client.ReadAsync(channel.BatchReadRegister, channel.BatchReadLength)
                .ConfigureAwait(false);
            var buffer = batchData.Content;
            foreach (var dataPoint in channel.DataPoints)
            {
                var value = TransValue(client, buffer, dataPoint.Index, dataPoint.StringByteLength, dataPoint.DataType,
                    dataPoint.Encoding);
                dataMessage.AddDataValue(dataPoint.FieldName, value);
            }
        }
        else
        {
            foreach (var dataPoint in channel.DataPoints)
            {
                var value = await ReadPlcValueAsync(
                    client,
                    dataPoint.Register,
                    dataPoint.DataType,
                    dataPoint.StringByteLength,
                    dataPoint.Encoding).ConfigureAwait(false);
                dataMessage.AddDataValue(dataPoint.FieldName, value);
            }
        }
    }

    /// <summary>
    ///     异步处理数据消息（表达式计算和发布），不阻塞采集循环。
    /// </summary>
    private async Task ProcessAndPublishMessageAsync(DeviceConfig config, DataAcquisitionChannel channel,
        DataMessage dataMessage)
    {
        try
        {
            await EvaluateAsync(dataMessage, channel.DataPoints).ConfigureAwait(false);
            await _queue.PublishAsync(dataMessage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(config.PLCCode, channel.Measurement, channel.ChannelCode);
            _logger.LogError(ex, "{PLCCode}-{Measurement}:异步处理数据消息失败: {Message}", config.PLCCode, channel.Measurement,
                ex.Message);
        }
    }

    /// <summary>
    ///     异步发布结束事件消息。
    /// </summary>
    private async Task PublishEndEventMessageAsync(DeviceConfig config, DataAcquisitionChannel channel,
        DataMessage dataMessage)
    {
        try
        {
            await _queue.PublishAsync(dataMessage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{PLCCode}-{Measurement}:发布结束事件消息失败: {Message}", config.PLCCode, channel.Measurement,
                ex.Message);
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
        IPLCClientService client,
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
    private static dynamic? TransValue(IPLCClientService client, byte[] buffer, int index, int length, string dataType,
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
    ///     判断是否应该触发采集。
    ///     触发条件说明：
    ///     - <see cref="AcquisitionTrigger.RisingEdge" />: 当生产序号从 0 变为非 0 时触发开始事件
    ///     - <see cref="AcquisitionTrigger.FallingEdge" />: 当生产序号从非 0 变为 0 时触发结束事件
    ///     特殊处理：
    ///     - 如果 mode 为 null，返回 false（不触发）
    ///     - 如果 previousValue 或 currentValue 为 null（首次读取），返回 true（默认触发首次读取）
    ///     - 所有数值比较使用 decimal 类型进行转换，确保浮点数和整数都能正确比较
    ///     使用场景：
    ///     - 用于条件采集模式（<see cref="AcquisitionMode.Conditional" />），判断是否应该触发 Start 或 End 事件
    ///     - Start 和 End 可以配置不同的触发条件，实现灵活的条件采集逻辑
    /// </summary>
    /// <param name="mode">触发模式。可选值：RisingEdge（生产序号从0变非0触发开始）、FallingEdge（生产序号从非0变0触发结束）。null 表示不触发。</param>
    /// <param name="previousValue">前一个读取的值，用于比较状态变化。null 表示首次读取。</param>
    /// <param name="currentValue">当前读取的值，用于比较状态变化。null 表示读取失败或无效值。</param>
    /// <returns>
    ///     如果应该触发采集则返回 true，否则返回 false。
    ///     - 如果 previousValue 或 currentValue 为 null（首次读取），默认返回 true
    ///     - 如果 mode 为 null，返回 false
    /// </returns>
    /// <example>
    ///     <code>
    /// // 生产序号从 0 变为非 0 时触发开始事件
    /// ShouldTrigger(AcquisitionTrigger.RisingEdge, 0, 1)  // 返回 true
    /// ShouldTrigger(AcquisitionTrigger.RisingEdge, 0, 5)  // 返回 true
    /// ShouldTrigger(AcquisitionTrigger.RisingEdge, 2, 3)  // 返回 true
    /// ShouldTrigger(AcquisitionTrigger.RisingEdge, 5, 1)  // 返回 false
    /// 
    /// // 生产序号从非 0 变为 0 时触发结束事件
    /// ShouldTrigger(AcquisitionTrigger.FallingEdge, 1, 0)  // 返回 true
    /// ShouldTrigger(AcquisitionTrigger.FallingEdge, 5, 0)  // 返回 true
    /// ShouldTrigger(AcquisitionTrigger.FallingEdge, 3, 2)  // 返回 true
    /// ShouldTrigger(AcquisitionTrigger.FallingEdge, 1, 5)  // 返回 false
    /// </code>
    /// </example>
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