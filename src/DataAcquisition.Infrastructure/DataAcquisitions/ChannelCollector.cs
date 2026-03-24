using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
///     通道采集器。根据配置从 Plc 读取数据，支持无条件和条件采集模式，将数据发布到队列。
/// </summary>
public class ChannelCollector : IChannelCollector
{
    private const string DiagnosticMeasurementSuffix = "_diagnostic";
    private readonly int _connectionCheckRetryDelayMs;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ILogger<ChannelCollector> _logger;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly IQueueService _queue;
    private readonly IAcquisitionStateManager _stateManager;
    private readonly int _triggerWaitDelayMs;

    // 采集频率统计（per-channel，避免跨通道竞态）
    private readonly ConcurrentDictionary<string, (int Count, long LastTicks)> _rateStats = new();

    /// <summary>
    ///     初始化通道采集器。
    /// </summary>
    public ChannelCollector(
        IHeartbeatMonitor heartbeatMonitor,
        ILogger<ChannelCollector> logger,
        IQueueService queue,
        IAcquisitionStateManager stateManager,
        IOptions<AcquisitionOptions> acquisitionOptions,
        IMetricsCollector? metricsCollector = null)
    {
        _heartbeatMonitor = heartbeatMonitor;
        _logger = logger;
        _queue = queue;
        _stateManager = stateManager;
        _metricsCollector = metricsCollector;

        var options = acquisitionOptions.Value.ChannelCollector;
        _connectionCheckRetryDelayMs = options.ConnectionCheckRetryDelayMs;
        _triggerWaitDelayMs = options.TriggerWaitDelayMs;
    }

    /// <summary>
    ///     按通道配置执行采集任务。支持 Always（持续）和 Conditional（边沿触发）两种模式。
    /// </summary>
    public async Task CollectAsync(DeviceConfig config, DataAcquisitionChannel dataAcquisitionChannel,
        IPlcDataAccessClient client, CancellationToken ct = default)
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
            var timestamp = DateTimeOffset.UtcNow;
            if (dataAcquisitionChannel.AcquisitionMode == AcquisitionMode.Always)
                await HandleUnconditionalCollectionAsync(config, dataAcquisitionChannel, client, timestamp, ct)
                    .ConfigureAwait(false);
            else if (dataAcquisitionChannel.AcquisitionMode == AcquisitionMode.Conditional)
                prevValue = await HandleConditionalCollectionAsync(config, dataAcquisitionChannel, client,
                    timestamp, prevValue, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     处理无条件采集。读取数据并按配置频率延迟。
    /// </summary>
    private async Task HandleUnconditionalCollectionAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPlcDataAccessClient client,
        DateTimeOffset timestamp,
        CancellationToken ct)
    {
        await HandleUnconditionalEventAsync(config.PlcCode, channel, client, timestamp).ConfigureAwait(false);
        // AcquisitionInterval = 0 表示最高频率采集（无延迟），> 0 表示延迟指定毫秒数
        if (channel.AcquisitionInterval > 0) await Task.Delay(channel.AcquisitionInterval, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     处理条件采集。监控触发条件，触发后会调用 HandleStartEventAsync 和 HandleEndEventAsync。
    /// </summary>
    private async Task<object?> HandleConditionalCollectionAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPlcDataAccessClient client,
        DateTimeOffset timestamp,
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
        var curr = await PlcValueAccessor.ReadAsync(client, conditionalAcq.Register, conditionalAcq.DataType)
            .ConfigureAwait(false);

        // 首次读取先做恢复判定，再建立基线，避免服务启动瞬间产生伪周期。
        if (prevValue == null)
        {
            await HandleRecoveryOnFirstSampleAsync(config.PlcCode, channel, client, timestamp, curr)
                .ConfigureAwait(false);
            await Task.Delay(_triggerWaitDelayMs, ct).ConfigureAwait(false);
            return curr;
        }

        // 评估触发条件
        var shouldStartTrigger = PlcValueAccessor.ShouldTrigger(conditionalAcq.StartTriggerMode, prevValue, curr);
        var shouldEndTrigger = PlcValueAccessor.ShouldTrigger(conditionalAcq.EndTriggerMode, prevValue, curr);

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
        IPlcDataAccessClient client,
        DateTimeOffset timestamp)
    {
        var sw = Stopwatch.StartNew();
        await HandleStartEventAsync(plcCode, channel, client, timestamp).ConfigureAwait(false);
        sw.Stop();

        RecordCollectionMetrics(plcCode, channel, sw.ElapsedMilliseconds);
    }

    private async Task HandleRecoveryOnFirstSampleAsync(
        string plcCode,
        DataAcquisitionChannel channel,
        IPlcDataAccessClient client,
        DateTimeOffset timestamp,
        object? currentValue)
    {
        var activeCycle = _stateManager.GetActiveCycle(plcCode, channel.ChannelCode, channel.Measurement);
        var isActive = PlcValueAccessor.IsTriggerActive(currentValue);

        if (activeCycle != null && isActive)
        {
            await PublishRecoveryDiagnosticAsync(plcCode, channel, client, timestamp, activeCycle, DiagnosticEventType.RecoveredStart)
                .ConfigureAwait(false);
            return;
        }

        if (activeCycle != null && !isActive)
        {
            var interruptedCycle = _stateManager.EndCycle(plcCode, channel.ChannelCode, channel.Measurement);
            if (interruptedCycle != null)
                await PublishRecoveryDiagnosticAsync(plcCode, channel, client, timestamp, interruptedCycle, DiagnosticEventType.Interrupted)
                    .ConfigureAwait(false);
            return;
        }

        if (activeCycle == null && isActive)
        {
            var recoveredCycle = _stateManager.StartCycle(plcCode, channel.ChannelCode, channel.Measurement);
            await PublishRecoveryDiagnosticAsync(plcCode, channel, client, timestamp, recoveredCycle, DiagnosticEventType.RecoveredStart)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     记录采集指标（延迟和频率）。
    /// </summary>
    private void RecordCollectionMetrics(string plcCode, DataAcquisitionChannel channel, long elapsedMilliseconds)
    {
        if (_metricsCollector == null) return;

        _metricsCollector.RecordCollectionLatency(plcCode, channel.ChannelCode, channel.Measurement,
            elapsedMilliseconds);

        var key = $"{plcCode}:{channel.ChannelCode}:{channel.Measurement}";
        var now = DateTimeOffset.UtcNow.Ticks;
        var updated = _rateStats.AddOrUpdate(key,
            _ => (1, now),
            (_, prev) =>
            {
                var elapsed = (now - prev.LastTicks) / (double)TimeSpan.TicksPerSecond;
                if (elapsed >= 1.0)
                {
                    var rate = (prev.Count + 1) / elapsed;
                    _metricsCollector.RecordCollectionRate(plcCode, channel.ChannelCode, channel.Measurement, rate);
                    return (0, now);
                }
                return (prev.Count + 1, prev.LastTicks);
            });
    }

    /// <summary>处理无条件采集事件：生成 CycleId → 读取数据 → 异步发布。</summary>
    private async Task HandleUnconditionalEventAsync(
        string plcCode,
        DataAcquisitionChannel channel,
        IPlcDataAccessClient client,
        DateTimeOffset timestamp)
    {
        try
        {
            var cycleId = Guid.NewGuid().ToString();
            var dataMessage = DataMessage.Create(cycleId, channel.Measurement, plcCode, channel.ChannelCode,
                EventType.Data, timestamp);

            await PopulateAndPublishAsync(plcCode, channel, client, dataMessage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(plcCode, channel.ChannelCode, channel.Measurement);
            _logger.LogError(ex, "{PlcCode}-{ChannelCode}-{Measurement}:采集异常: {Message}", plcCode, channel.ChannelCode,
                channel.Measurement, ex.Message);
        }
    }

    /// <summary>处理条件采集的开始事件：StartCycle → 读取数据 → 异步发布。</summary>
    private async Task HandleStartEventAsync(
        string plcCode,
        DataAcquisitionChannel channel,
        IPlcDataAccessClient client,
        DateTimeOffset timestamp)
    {
        try
        {
            var cycle = _stateManager.StartCycle(
                plcCode,
                channel.ChannelCode,
                channel.Measurement);
            var dataMessage = DataMessage.Create(cycle.CycleId, channel.Measurement, plcCode,
                channel.ChannelCode, EventType.Start, timestamp);
            await PopulateAndPublishAsync(plcCode, channel, client, dataMessage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(plcCode, channel.ChannelCode, channel.Measurement);
            _logger.LogError(ex, "{PlcCode}-{ChannelCode}-{Measurement}:采集异常: {Message}", plcCode, channel.ChannelCode,
                channel.Measurement, ex.Message);
        }
    }

    /// <summary>
    ///     处理条件采集的结束事件：结束采集周期并发布 End 消息。
    /// </summary>
    private async Task HandleEndEventAsync(string plcCode,
        DataAcquisitionChannel channel,
        DateTimeOffset timestamp)
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

    private async Task PublishRecoveryDiagnosticAsync(
        string plcCode,
        DataAcquisitionChannel channel,
        IPlcDataAccessClient client,
        DateTimeOffset timestamp,
        AcquisitionCycle cycle,
        DiagnosticEventType diagnosticType)
    {
        try
        {
            var dataMessage = DataMessage.CreateDiagnostic(
                cycle.CycleId,
                GetDiagnosticMeasurement(channel.Measurement),
                plcCode,
                channel.ChannelCode,
                diagnosticType,
                timestamp);

            dataMessage.AddDataValue("source_measurement", channel.Measurement);
            await PopulateAndPublishAsync(plcCode, channel, client, dataMessage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(plcCode, channel.ChannelCode, channel.Measurement);
            _logger.LogError(ex,
                "{PlcCode}-{ChannelCode}-{Measurement}:恢复诊断事件发布失败: {DiagnosticType}",
                plcCode, channel.ChannelCode, channel.Measurement, diagnosticType);
        }
    }

    private async Task PopulateAndPublishAsync(
        string plcCode,
        DataAcquisitionChannel channel,
        IPlcDataAccessClient client,
        DataMessage dataMessage)
    {
        if (dataMessage.DiagnosticType.HasValue)
            dataMessage.AddDataValue("source_measurement", channel.Measurement);

        await ChannelMetricReader.ReadAsync(client, channel, dataMessage, _logger).ConfigureAwait(false);
        _ = EvaluateAndPublishAsync(plcCode, channel, dataMessage);
    }

    /// <summary>
    ///     异步处理数据消息（表达式计算和发布），不阻塞采集循环。
    /// </summary>
    private async Task EvaluateAndPublishAsync(string plcCode, DataAcquisitionChannel channel,
        DataMessage dataMessage)
    {
        try
        {
            await MetricExpressionEvaluator.EvaluateAsync(dataMessage, channel.Metrics, _logger).ConfigureAwait(false);
            await _queue.PublishAsync(dataMessage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(plcCode, channel.ChannelCode, channel.Measurement);
            _logger.LogError(ex, "{PlcCode}-{ChannelCode}-{Measurement}:异步处理数据消息失败: {Message}", plcCode,
                channel.ChannelCode, channel.Measurement, ex.Message);
        }
    }

    private static string GetDiagnosticMeasurement(string measurement) =>
        $"{measurement}{DiagnosticMeasurementSuffix}";

}
