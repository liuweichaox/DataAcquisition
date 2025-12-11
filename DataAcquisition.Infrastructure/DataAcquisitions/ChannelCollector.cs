using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Configuration;
using NCalc;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
/// 通道采集器，根据配置从 PLC 读取数据并发布。
/// 职责：
/// - 监控PLC寄存器状态，判断触发条件
/// - 执行数据采集（批量读取或单点读取）
/// - 管理采集周期状态（通过IAcquisitionStateManager）
/// - 发布数据消息到队列
/// </summary>
public class ChannelCollector : IChannelCollector
{
    private readonly IPLCStateManager _plcStateManager;
    private readonly IOperationalEventsService _events;
    private readonly IQueueService _queue;
    private readonly IAcquisitionStateManager _stateManager;
    private readonly ITriggerEvaluationService _triggerEvaluationService;
    private readonly IMetricsCollector? _metricsCollector;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private DateTime _lastCollectionTime = DateTime.Now;
    private int _collectionCount = 0;
    private readonly object _rateLock = new object();
    private readonly int _connectionCheckRetryDelayMs;
    private readonly int _triggerWaitDelayMs;

    /// <summary>
    /// 初始化通道采集器。
    /// </summary>
    public ChannelCollector(
        IPLCStateManager plcStateManager,
        IOperationalEventsService events,
        IQueueService queue,
        IAcquisitionStateManager stateManager,
        ITriggerEvaluationService triggerEvaluationService,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        IMetricsCollector? metricsCollector = null)
    {
        _plcStateManager = plcStateManager;
        _events = events;
        _queue = queue;
        _stateManager = stateManager;
        _triggerEvaluationService = triggerEvaluationService;
        _metricsCollector = metricsCollector;

        var options = new Domain.Models.ChannelCollectorOptions
        {
            ConnectionCheckRetryDelayMs = int.TryParse(configuration["Acquisition:ChannelCollector:ConnectionCheckRetryDelayMs"], out var retryDelay) ? retryDelay : 100,
            TriggerWaitDelayMs = int.TryParse(configuration["Acquisition:ChannelCollector:TriggerWaitDelayMs"], out var waitDelay) ? waitDelay : 100
        };
        _connectionCheckRetryDelayMs = options.ConnectionCheckRetryDelayMs;
        _triggerWaitDelayMs = options.TriggerWaitDelayMs;
    }

    /// <summary>
    /// 按通道配置执行采集任务。
    /// </summary>
    public async Task CollectAsync(DeviceConfig config, DataAcquisitionChannel dataAcquisitionChannel, IPlcClientService client, CancellationToken ct = default)
    {
        await Task.Yield();
        object? prevValue = null;
        while (!ct.IsCancellationRequested)
        {
            if (!_plcStateManager.PlcConnectionHealth.TryGetValue(config.PLCCode, out var isConnected) || !isConnected)
            {
                await Task.Delay(_connectionCheckRetryDelayMs, ct).ConfigureAwait(false);
                continue;
            }

            if (!_plcStateManager.PlcLocks.TryGetValue(config.PLCCode, out var locker))
            {
                await _events.ErrorAsync($"{config.PLCCode}-未找到锁对象，跳过本次采集", null).ConfigureAwait(false);
                await Task.Delay(_connectionCheckRetryDelayMs, ct).ConfigureAwait(false);
                continue;
            }

            await locker.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                var timestamp = DateTime.Now;
                if (dataAcquisitionChannel.AcquisitionMode == AcquisitionMode.Always)
                {
                    await HandleUnconditionalEventAsync(config, dataAcquisitionChannel, client, timestamp, ct).ConfigureAwait(false);
                    // 无条件采集时，根据配置的采集频率进行延迟
                    // AcquisitionInterval = 0 表示最高频率采集（无延迟），> 0 表示延迟指定毫秒数
                    if (dataAcquisitionChannel.AcquisitionInterval > 0)
                    {
                        await Task.Delay(dataAcquisitionChannel.AcquisitionInterval, ct).ConfigureAwait(false);
                    }
                } 
                else if (dataAcquisitionChannel.AcquisitionMode == AcquisitionMode.Conditional)
                {
                    // 检查 ConditionalAcquisition 是否为 null
                    if (dataAcquisitionChannel.ConditionalAcquisition == null)
                    {
                        continue; // 跳过后续的条件采集逻辑
                    }

                    var startCfg = dataAcquisitionChannel.ConditionalAcquisition.StartTriggerMode;
                    var endCfg = dataAcquisitionChannel.ConditionalAcquisition.EndTriggerMode;
                    var register = dataAcquisitionChannel.ConditionalAcquisition.Register;
                    var dataType = dataAcquisitionChannel.ConditionalAcquisition.DataType;

                    // 验证必要字段
                    if (string.IsNullOrWhiteSpace(register) || string.IsNullOrWhiteSpace(dataType))
                    {
                        await _events.ErrorAsync($"{config.PLCCode}-{dataAcquisitionChannel.Measurement}:条件采集配置不完整，Register或DataType为空", null).ConfigureAwait(false);
                        await Task.Delay(_triggerWaitDelayMs, ct).ConfigureAwait(false);
                        continue;
                    }

                    object? curr = await ReadPlcValueAsync(client, register, dataType);
                    
                    // 评估触发条件（ShouldTrigger 内部会检查 mode 是否为 null）
                    var shouldStartTrigger = _triggerEvaluationService.ShouldTrigger(startCfg, prevValue, curr);
                    var shouldEndTrigger = _triggerEvaluationService.ShouldTrigger(endCfg, prevValue, curr);

                    // 优先处理结束事件（如果同时触发，先结束当前周期，再开始新周期）
                    if (shouldEndTrigger)
                    {
                        await HandleEndEventAsync(config, dataAcquisitionChannel, timestamp, ct).ConfigureAwait(false);
                    }
                    
                    if (shouldStartTrigger)
                    {
                        _stopwatch.Restart();
                        await HandleStartEventAsync(config, dataAcquisitionChannel, client, timestamp, ct).ConfigureAwait(false);
                        // 记录采集延迟和频率
                        _stopwatch.Stop();
                        
                        if (_metricsCollector != null)
                        {
                            _metricsCollector.RecordCollectionLatency(config.PLCCode, dataAcquisitionChannel.Measurement, _stopwatch.ElapsedMilliseconds, dataAcquisitionChannel.ChannelCode);

                            lock (_rateLock)
                            {
                                _collectionCount++;
                                var elapsed = (DateTime.Now - _lastCollectionTime).TotalSeconds;
                                if (elapsed >= 1.0) // 每秒更新一次频率
                                {
                                    var rate = _collectionCount / elapsed;
                                    _metricsCollector.RecordCollectionRate(config.PLCCode, dataAcquisitionChannel.Measurement, rate, dataAcquisitionChannel.ChannelCode);
                                    _collectionCount = 0;
                                    _lastCollectionTime = DateTime.Now;
                                }
                            }
                        }
                    }
                    
                    // 无论是否触发事件，都需要延迟和更新 prevValue
                    // 这样可以避免CPU空转，同时保存当前值用于下次比较（检测边沿变化）
                    await Task.Delay(_triggerWaitDelayMs, ct).ConfigureAwait(false);
                    prevValue = curr; // 保存当前值，用于下次循环时比较（判断上升沿/下降沿等）
                }
            }
            finally
            {
                locker.Release();
            }
        }
    }

    /// <summary>
    /// 对数据消息进行表达式计算并记录异常。
    /// </summary>
    private async Task EvaluateAsync(DataMessage dataMessage, List<DataPoint>? dataPoints)
    {
        await Task.Yield();
        try
        {
            if (dataPoints == null) return;

            foreach (var kv in dataMessage.DataValues.ToList())
            {
                if (!IsNumberType(kv.Value)) continue;

                var register = dataPoints.SingleOrDefault(x => x.FieldName == kv.Key);
                if (register == null || string.IsNullOrWhiteSpace(register.EvalExpression) || kv.Value == null) continue;

                var evalExpression = register.EvalExpression;
                if (string.IsNullOrWhiteSpace(evalExpression)) continue;

                // kv.Value 已经在上面的 null 检查中验证，使用 ! 断言非空
                var valueToEval = kv.Value;

                var expression = new AsyncExpression(evalExpression)
                {
                    Parameters =
                    {
                        ["value"] = valueToEval
                    }
                };

                var value = await expression.EvaluateAsync().ConfigureAwait(false);
                dataMessage.AddDataValue(kv.Key, value ?? 0);
            }
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync($"Error handling data point: {ex.Message}", ex).ConfigureAwait(false);
        }
    }

    
    /// <summary>
    /// 处理无条件事件：读取数据并发布消息。
    /// </summary>
    private async Task HandleUnconditionalEventAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPlcClientService client,
        DateTime timestamp,
        CancellationToken ct)
    {
        try
        {
            string cycleId = Guid.NewGuid().ToString();
            var dataMessage = DataMessage.Create(cycleId, channel.Measurement,config.PLCCode,channel.ChannelCode, EventType.Data, timestamp, channel.BatchSize);
            
            // 读取数据点
            await ReadDataPointsAsync(client, channel, dataMessage).ConfigureAwait(false);

            // 异步处理表达式计算并发布消息，不阻塞采集循环
            // 使用 Task.Run 确保采集循环可以立即继续下一次采集，提高吞吐量
            _ = Task.Run(async () =>
            {
                try
                {
                    await EvaluateAsync(dataMessage, channel.DataPoints).ConfigureAwait(false);
                    await _queue.PublishAsync(dataMessage).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _metricsCollector?.RecordError(config.PLCCode, channel.Measurement, channel.ChannelCode);
                    await _events.ErrorAsync($"{config.PLCCode}-{channel.Measurement}:异步处理数据消息失败: {ex.Message}", ex).ConfigureAwait(false);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(config.PLCCode, channel.Measurement);
            await _events.ErrorAsync($"{config.PLCCode}-{channel.Measurement}:采集异常: {ex.Message}", ex).ConfigureAwait(false);
        }
    }
    
    /// <summary>
    /// 处理开始事件：生成采集周期，读取数据并发布消息。
    /// </summary>
    private async Task HandleStartEventAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPlcClientService client,
        DateTime timestamp,
        CancellationToken ct)
    {
        try
        {
            var cycle = _stateManager.StartCycle(
                config.PLCCode,
                channel.Measurement,
                channel.ChannelCode);
            var dataMessage = DataMessage.Create(cycle.CycleId, channel.Measurement, config.PLCCode, channel.ChannelCode, EventType.Start, timestamp, channel.BatchSize);
            
            // 读取数据点
            await ReadDataPointsAsync(client, channel, dataMessage).ConfigureAwait(false);

            // 异步处理表达式计算并发布消息，不阻塞采集循环
            // 使用 Task.Run 确保采集循环可以立即继续下一次采集，提高吞吐量
            _ = Task.Run(async () =>
            {
                try
                {
                    await EvaluateAsync(dataMessage, channel.DataPoints).ConfigureAwait(false);
                    await _queue.PublishAsync(dataMessage).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _metricsCollector?.RecordError(config.PLCCode, channel.Measurement, channel.ChannelCode);
                    await _events.ErrorAsync($"{config.PLCCode}-{channel.Measurement}:异步处理数据消息失败: {ex.Message}", ex).ConfigureAwait(false);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _metricsCollector?.RecordError(config.PLCCode, channel.Measurement);
            await _events.ErrorAsync($"{config.PLCCode}-{channel.Measurement}:采集异常: {ex.Message}", ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 处理结束事件：结束采集周期，写入End事件数据点。
    /// </summary>
    /// <returns>如果应该跳过后续处理则返回true，否则返回false</returns>
    private async Task<bool> HandleEndEventAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        DateTime timestamp,
        CancellationToken ct)
    {
        try
        {
            // 结束采集周期，获取CycleId用于关联Start事件
            var cycle = _stateManager.EndCycle(config.PLCCode, channel.Measurement);
            if (cycle == null)
            {
                // 异常情况：找不到对应的cycle，记录警告并跳过
                await _events.ErrorAsync(
                    $"{config.PLCCode}-{channel.Measurement} " +
                    $"End事件触发但找不到对应的采集周期，可能Start事件未正确触发或系统重启导致状态丢失",
                    null).ConfigureAwait(false);
                return true; // 需要跳过后续处理
            }

            // 创建End事件数据点（时序数据库不支持Update，改为写入新数据点）
            var dataMessage = DataMessage.Create(cycle.CycleId, channel.Measurement, config.PLCCode, channel.ChannelCode, EventType.End, timestamp, channel.BatchSize);
            _ = Task.Run(async () =>
            {
                try
                {
                    await _queue.PublishAsync(dataMessage).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _events.ErrorAsync($"{config.PLCCode}-{channel.Measurement}:发布结束事件消息失败: {ex.Message}", ex).ConfigureAwait(false);
                }
            }, ct);

            return false; // 正常处理，不需要跳过
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync($"{config.PLCCode}-{channel.Measurement}:采集异常: {ex.Message}", ex).ConfigureAwait(false);
            return false;
        }
    }

    /// <summary>
    /// 读取数据点：支持批量读取和单点读取两种方式。
    /// </summary>
    private async Task ReadDataPointsAsync(
        IPlcClientService client,
        DataAcquisitionChannel channel,
        DataMessage dataMessage)
    {
        if (channel.DataPoints == null) return;

        if (channel.EnableBatchRead)
        {
            var batchData = await client.ReadAsync(channel.BatchReadRegister, channel.BatchReadLength).ConfigureAwait(false);
            var buffer = batchData.Content;
            foreach (var dataPoint in channel.DataPoints)
            {
                var value = TransValue(client, buffer, dataPoint.Index, dataPoint.StringByteLength, dataPoint.DataType, dataPoint.Encoding);
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
    /// 判断对象是否为数值类型。
    /// </summary>
    private static bool IsNumberType(object? value)
    {
        return value is ushort or uint or ulong or short or int or long or float or double;
    }


    /// <summary>
    /// 读取指定寄存器的值。
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
            "string" => await client.ReadStringAsync(register, (ushort)stringLength, Encoding.GetEncoding(encoding ?? "UTF8")).ConfigureAwait(false),
            "bool" => await client.ReadBoolAsync(register).ConfigureAwait(false),
            _ => throw new NotSupportedException($"不支持的数据类型: {dataType}")
        };
    }

    /// <summary>
    /// 按数据类型转换缓冲区中的值。
    /// </summary>
    private static dynamic? TransValue(IPlcClientService client, byte[] buffer, int index, int length, string dataType, string encoding)
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
}
