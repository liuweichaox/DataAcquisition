using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

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
    public async Task CollectAsync(DeviceConfig config, Channel channel, IPlcClientService client, CancellationToken ct = default)
    {
        await Task.Yield();
        object? prevVal = null;
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
                var trigger = channel.Trigger;
                var currVal = trigger.Mode == TriggerMode.Always
                    ? null
                    : await ReadPlcValueAsync(client, trigger.Register, trigger.DataType);

                if (!ShouldSample(trigger.Mode, prevVal, currVal))
                {
                    await Task.Delay(100, ct);
                    continue;
                }

                try
                {
                    var operation = trigger.Operation;
                    var key = $"{config.Code}:{channel.TableName}";
                    var timestamp = DateTime.Now;
                    var dataMessage = new DataMessage(timestamp, channel.TableName, channel.BatchSize, channel.DataPoints, operation);
                    if (operation == DataOperation.Insert)
                    {
                        if (channel.EnableBatchRead)
                        {
                            var batchData = await client.ReadAsync(channel.BatchReadRegister, channel.BatchReadLength);
                            var buffer = batchData.Content;
                            if (channel.DataPoints != null)
                            {
                                foreach (var dataPoint in channel.DataPoints)
                                {
                                    var value = TransValue(client, buffer, dataPoint.Index, dataPoint.StringByteLength, dataPoint.DataType, dataPoint.Encoding);
                                    dataMessage.DataValues[dataPoint.ColumnName] = value;
                                }
                            }
                        }
                        else if (channel.DataPoints != null)
                        {
                            foreach (var dataPoint in channel.DataPoints)
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

                        if (!string.IsNullOrEmpty(trigger.TimeColumnName))
                        {
                            dataMessage.DataValues[trigger.TimeColumnName] = timestamp;
                            _lastStartTimes[key] = timestamp;
                            _lastStartTimeColumns[key] = trigger.TimeColumnName;
                        }

                        await _queue.PublishAsync(dataMessage);
                    }
                    else if (_lastStartTimes.TryRemove(key, out var startTime))
                    {
                        if (_lastStartTimeColumns.TryRemove(key, out var startColumn))
                        {
                            dataMessage.KeyValues[startColumn] = startTime;
                        }

                        if (!string.IsNullOrEmpty(trigger.TimeColumnName))
                        {
                            dataMessage.DataValues[trigger.TimeColumnName] = timestamp;
                        }

                        await _queue.PublishAsync(dataMessage);
                    }
                }
                catch (Exception ex)
                {
                    await _events.ErrorAsync(channel.ChannelName, $"[{channel.ChannelName}:{channel.TableName}]采集异常: {ex.Message}", ex);
                }

                prevVal = currVal;
            }
            finally
            {
                locker.Release();
            }
        }
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
