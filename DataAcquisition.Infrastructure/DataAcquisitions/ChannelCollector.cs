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
/// </summary>
public class ChannelCollector : IChannelCollector
{
    private readonly IPlcStateManager _plcStateManager;
    private readonly IOperationalEventsService _events;
    private readonly IQueueService _queue;
    private readonly ConcurrentDictionary<string, DateTime> _lastStartTimes = new();
    private readonly ConcurrentDictionary<string, string> _lastStartTimeColumns = new();

    /// <summary>
    /// 初始化通道采集器。
    /// </summary>
    public ChannelCollector(IPlcStateManager plcStateManager, IOperationalEventsService events, IQueueService queue)
    {
        _plcStateManager = plcStateManager;
        _events = events;
        _queue = queue;
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
                await Task.Delay(100, ct);
                continue;
            }

            var locker = _plcStateManager.PlcLocks[config.Code];
            await locker.WaitAsync(ct);

            try
            {
                var startCfg = dataAcquisitionChannel.Lifecycle?.Start ?? new LifecycleEvent
                {
                    TriggerMode = TriggerMode.Always,
                    Operation = DataOperation.Insert
                };
                var endCfg = dataAcquisitionChannel.Lifecycle?.End;
                var register = dataAcquisitionChannel.Lifecycle?.Register;
                var dataType = dataAcquisitionChannel.Lifecycle?.DataType;

                var needRead = register != null && dataType != null &&
                              (startCfg.TriggerMode != TriggerMode.Always || (endCfg != null && endCfg.TriggerMode != TriggerMode.Always));
                object? curr = null;
                if (needRead)
                {
                    curr = await ReadPlcValueAsync(client, register!, dataType!);
                }

                var fireStart = register != null && dataType != null && ShouldSample(startCfg.TriggerMode, prevValue, curr);
                var fireEnd = endCfg != null && register != null && dataType != null && ShouldSample(endCfg.TriggerMode, prevValue, curr);

                if (!fireStart && !fireEnd)
                {
                    await Task.Delay(100, ct);
                    prevValue = curr;
                    continue;
                }

                var key = $"{config.Code}:{dataAcquisitionChannel.TableName}";
                var timestamp = DateTime.Now;

                if (fireStart)
                {
                    try
                    {
                        var dataMessage = new DataMessage(timestamp, dataAcquisitionChannel.TableName, dataAcquisitionChannel.BatchSize, startCfg.Operation);
                        if (startCfg.Operation == DataOperation.Insert)
                        {
                            if (dataAcquisitionChannel.EnableBatchRead)
                            {
                                var batchData = await client.ReadAsync(dataAcquisitionChannel.BatchReadRegister, dataAcquisitionChannel.BatchReadLength);
                                var buffer = batchData.Content;
                                if (dataAcquisitionChannel.DataPoints != null)
                                {
                                    foreach (var dataPoint in dataAcquisitionChannel.DataPoints)
                                    {
                                        var value = TransValue(client, buffer, dataPoint.Index, dataPoint.StringByteLength, dataPoint.DataType, dataPoint.Encoding);
                                        dataMessage.DataValues[dataPoint.ColumnName] = value;
                                    }
                                }
                            }
                            else if (dataAcquisitionChannel.DataPoints != null)
                            {
                                foreach (var dataPoint in dataAcquisitionChannel.DataPoints)
                                {
                                    var value = await ReadPlcValueAsync(
                                        client,
                                        dataPoint.Register,
                                        dataPoint.DataType,
                                        dataPoint.StringByteLength,
                                        dataPoint.Encoding);
                                    dataMessage.DataValues[dataPoint.ColumnName] = value;
                                }
                            }

                            if (!string.IsNullOrEmpty(startCfg.StampColumn))
                            {
                                dataMessage.DataValues[startCfg.StampColumn] = timestamp;
                                _lastStartTimes[key] = timestamp;
                                _lastStartTimeColumns[key] = startCfg.StampColumn;
                            }

                            _ = Task.Run(async () =>
                            {
                                await EvaluateAsync(dataMessage, dataAcquisitionChannel.DataPoints);
                                await _queue.PublishAsync(dataMessage);
                            }, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        await _events.ErrorAsync($"{config.Code}-{dataAcquisitionChannel.ChannelName}:{dataAcquisitionChannel.TableName}采集异常: {ex.Message}", ex);
                    }
                }

                if (fireEnd && endCfg != null)
                {
                    try
                    {
                        var dataMessage = new DataMessage(timestamp, dataAcquisitionChannel.TableName, dataAcquisitionChannel.BatchSize, endCfg.Operation);
                        if (_lastStartTimes.TryRemove(key, out var startTime))
                        {
                            if (_lastStartTimeColumns.TryRemove(key, out var startColumn))
                            {
                                dataMessage.KeyValues[startColumn] = startTime;
                            }
                        }

                        if (!string.IsNullOrEmpty(endCfg.StampColumn))
                        {
                            dataMessage.DataValues[endCfg.StampColumn] = timestamp;
                        }

                        _ = Task.Run(async () =>
                        {
                            await _queue.PublishAsync(dataMessage);
                        }, ct);

                    }
                    catch (Exception ex)
                    {
                        await _events.ErrorAsync($"{config.Code}-{dataAcquisitionChannel.ChannelName}:{dataAcquisitionChannel.TableName}采集异常: {ex.Message}", ex);
                    }
                }

                prevValue = curr;
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

                var expression = new AsyncExpression(register.EvalExpression)
                {
                    Parameters =
                    {
                        ["value"] = kv.Value
                    }
                };

                var value = await expression.EvaluateAsync();
                dataMessage.DataValues[kv.Key] = value ?? 0;
            }
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync($"Error handling data point: {ex.Message}- StackTrace: {ex.StackTrace}", ex);
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
    /// 根据触发模式判断是否采样。
    /// </summary>
    private static bool ShouldSample(TriggerMode mode, object? prev, object? curr)
    {
        if (prev == null || curr == null) return true;
        var p = Convert.ToDecimal(prev);
        var c = Convert.ToDecimal(curr);
        return mode switch
        {
            TriggerMode.Always => true,
            TriggerMode.ValueIncrease => p < c,
            TriggerMode.ValueDecrease => p > c,
            TriggerMode.RisingEdge => p == 0 && c == 1,
            TriggerMode.FallingEdge => p == 1 && c == 0,
            _ => false
        };
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
            "ushort" => await client.ReadUShortAsync(register),
            "uint" => await client.ReadUIntAsync(register),
            "ulong" => await client.ReadULongAsync(register),
            "short" => await client.ReadShortAsync(register),
            "int" => await client.ReadIntAsync(register),
            "long" => await client.ReadLongAsync(register),
            "float" => await client.ReadFloatAsync(register),
            "double" => await client.ReadDoubleAsync(register),
            "string" => await client.ReadStringAsync(register, (ushort)stringLength, Encoding.GetEncoding(encoding ?? "UTF8")),
            "bool" => await client.ReadBoolAsync(register),
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
