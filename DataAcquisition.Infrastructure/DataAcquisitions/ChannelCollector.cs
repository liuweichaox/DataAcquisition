using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
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
    private readonly IPlcStateManager _plcStateManager;
    private readonly IOperationalEventsService _events;
    private readonly IQueueService _queue;
    private readonly IAcquisitionStateManager _stateManager;
    private readonly ITriggerEvaluator _triggerEvaluator;

    /// <summary>
    /// 初始化通道采集器。
    /// </summary>
    public ChannelCollector(
        IPlcStateManager plcStateManager,
        IOperationalEventsService events,
        IQueueService queue,
        IAcquisitionStateManager stateManager,
        ITriggerEvaluator triggerEvaluator)
    {
        _plcStateManager = plcStateManager;
        _events = events;
        _queue = queue;
        _stateManager = stateManager;
        _triggerEvaluator = triggerEvaluator;
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
            if (!_plcStateManager.PlcConnectionHealth.TryGetValue(config.Code, out var isConnected) || !isConnected)
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
                continue;
            }

            if (!_plcStateManager.PlcLocks.TryGetValue(config.Code, out var locker))
            {
                await _events.ErrorAsync($"{config.Code}-未找到锁对象，跳过本次采集", null).ConfigureAwait(false);
                await Task.Delay(100, ct).ConfigureAwait(false);
                continue;
            }

            await locker.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                var startCfg = dataAcquisitionChannel.ConditionalAcquisition?.Start ?? new AcquisitionTrigger
                {
                    TriggerMode = TriggerMode.Always,
                    Operation = DataOperation.Insert
                };
                var endCfg = dataAcquisitionChannel.ConditionalAcquisition?.End;
                var register = dataAcquisitionChannel.ConditionalAcquisition?.Register;
                var dataType = dataAcquisitionChannel.ConditionalAcquisition?.DataType;

                var needRead = register != null && dataType != null &&
                              (startCfg.TriggerMode != TriggerMode.Always || (endCfg != null && endCfg.TriggerMode != TriggerMode.Always));
                object? curr = null;
                if (needRead)
                {
                    curr = await ReadPlcValueAsync(client, register!, dataType!);
                }

                var fireStart = register != null && dataType != null && _triggerEvaluator.ShouldTrigger(startCfg.TriggerMode, prevValue, curr);
                var fireEnd = endCfg != null && register != null && dataType != null && _triggerEvaluator.ShouldTrigger(endCfg.TriggerMode, prevValue, curr);

                // 无条件采集：ConditionalAcquisition 为 null 时，Always 模式且没有 register，直接触发采集
                var isUnconditionalAcquisition = dataAcquisitionChannel.ConditionalAcquisition == null;
                if (isUnconditionalAcquisition)
                {
                    fireStart = true; // 无条件采集总是触发
                }

                if (!fireStart && !fireEnd)
                {
                    await Task.Delay(100, ct).ConfigureAwait(false);
                    prevValue = curr;
                    continue;
                }

                var timestamp = DateTime.Now;

                if (fireStart)
                {
                    await HandleStartEventAsync(config, dataAcquisitionChannel, client, startCfg, timestamp, isUnconditionalAcquisition, ct).ConfigureAwait(false);
                }

                if (fireEnd && endCfg != null)
                {
                    var shouldContinue = await HandleEndEventAsync(config, dataAcquisitionChannel, endCfg, timestamp, ct).ConfigureAwait(false);
                    if (shouldContinue)
                    {
                        prevValue = curr;
                        continue;
                    }
                }

                prevValue = curr;

                // 无条件采集时，采集后需要延迟，避免过于频繁
                if (isUnconditionalAcquisition && fireStart)
                {
                    await Task.Delay(100, ct).ConfigureAwait(false);
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

                var register = dataPoints.SingleOrDefault(x => x.ColumnName == kv.Key);
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
                dataMessage.DataValues[kv.Key] = value ?? 0;
            }
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync($"Error handling data point: {ex.Message}", ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 处理开始事件：生成采集周期，读取数据并发布消息。
    /// </summary>
    /// <param name="isUnconditionalAcquisition">是否为无条件采集（ConditionalAcquisition 为 null）</param>
    private async Task HandleStartEventAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        IPlcClientService client,
        AcquisitionTrigger startCfg,
        DateTime timestamp,
        bool isUnconditionalAcquisition,
        CancellationToken ct)
    {
        try
        {
            var dataMessage = new DataMessage(timestamp, channel.TableName, channel.BatchSize, DataOperation.Insert);

            // 设置设备编码和通道名称（用于InfluxDB标签）
            dataMessage.DeviceCode = config.Code;
            dataMessage.ChannelName = channel.ChannelName;

            // 所有采集都生成 cycle_id
            string cycleId;
            if (isUnconditionalAcquisition)
            {
                // 无条件采集：直接生成 cycle_id，不需要状态管理
                cycleId = Guid.NewGuid().ToString();
                dataMessage.EventType = "data"; // 普通数据点
            }
            else
            {
                // 条件采集：使用状态管理器生成 cycle_id
                if (!string.IsNullOrEmpty(startCfg.StampColumn))
                {
                    var cycle = _stateManager.StartCycle(
                        config.Code,
                        channel.ChannelName,
                        channel.TableName);
                    cycleId = cycle.CycleId;
                    dataMessage.DataValues[startCfg.StampColumn] = cycle.StartTime;
                }
                else
                {
                    // 即使没有 StampColumn，也生成 cycle_id
                    cycleId = Guid.NewGuid().ToString();
                }
                dataMessage.EventType = "start"; // Start事件
            }

            // 设置 cycle_id
            dataMessage.CycleId = cycleId;
            dataMessage.DataValues["cycle_id"] = cycleId;

            // 读取数据点
            await ReadDataPointsAsync(client, channel, dataMessage).ConfigureAwait(false);

            // 异步处理表达式计算并发布消息
            _ = Task.Run(async () =>
            {
                try
                {
                    await EvaluateAsync(dataMessage, channel.DataPoints).ConfigureAwait(false);
                    await _queue.PublishAsync(dataMessage).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _events.ErrorAsync($"{config.Code}-{channel.ChannelName}:异步处理数据消息失败: {ex.Message}", ex).ConfigureAwait(false);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync($"{config.Code}-{channel.ChannelName}:{channel.TableName}采集异常: {ex.Message}", ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 处理结束事件：结束采集周期，写入End事件数据点。
    /// </summary>
    /// <returns>如果应该跳过后续处理则返回true，否则返回false</returns>
    private async Task<bool> HandleEndEventAsync(
        DeviceConfig config,
        DataAcquisitionChannel channel,
        AcquisitionTrigger endCfg,
        DateTime timestamp,
        CancellationToken ct)
    {
        try
        {
            // 结束采集周期，获取CycleId用于关联Start事件
            var cycle = _stateManager.EndCycle(config.Code, channel.TableName);
            if (cycle == null)
            {
                // 异常情况：找不到对应的cycle，记录警告并跳过
                await _events.ErrorAsync(
                    $"{config.Code}-{channel.ChannelName}:{channel.TableName} " +
                    $"End事件触发但找不到对应的采集周期，可能Start事件未正确触发或系统重启导致状态丢失",
                    null).ConfigureAwait(false);
                return true; // 需要跳过后续处理
            }

            // 创建End事件数据点（时序数据库不支持Update，改为写入新数据点）
            var dataMessage = new DataMessage(timestamp, channel.TableName, channel.BatchSize, DataOperation.Insert);
            dataMessage.DeviceCode = config.Code;
            dataMessage.ChannelName = channel.ChannelName;
            dataMessage.CycleId = cycle.CycleId;
            dataMessage.EventType = "end"; // End事件标记

            if (!string.IsNullOrEmpty(endCfg.StampColumn))
            {
                dataMessage.DataValues[endCfg.StampColumn] = timestamp;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await _queue.PublishAsync(dataMessage).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _events.ErrorAsync($"{config.Code}-{channel.ChannelName}:发布结束事件消息失败: {ex.Message}", ex).ConfigureAwait(false);
                }
            }, ct);

            return false; // 正常处理，不需要跳过
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync($"{config.Code}-{channel.ChannelName}:{channel.TableName}采集异常: {ex.Message}", ex).ConfigureAwait(false);
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
                dataMessage.DataValues[dataPoint.ColumnName] = value;
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
                dataMessage.DataValues[dataPoint.ColumnName] = value;
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
