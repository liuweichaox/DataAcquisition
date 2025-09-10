using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

public class ModuleCollector : IModuleCollector
{
    private readonly IPlcStateManager _plcStateManager;
    private readonly IOperationalEventsService _events;
    private readonly IQueueService _queue;
    private readonly ConcurrentDictionary<string, DateTime> _lastStartTimes = new();
    private readonly ConcurrentDictionary<string, string> _lastStartTimeColumns = new();

    public ModuleCollector(IPlcStateManager plcStateManager, IOperationalEventsService events, IQueueService queue)
    {
        _plcStateManager = plcStateManager;
        _events = events;
        _queue = queue;
    }

    public async Task CollectAsync(DeviceConfig config, Module module, IPlcClientService client, CancellationToken ct = default)
    {
        await Task.Yield();
        object? prevVal = null;
        while (!ct.IsCancellationRequested)
        {
            if (!_plcStateManager.PlcConnectionHealth.TryGetValue(config.Code, out var isConnected) || !isConnected)
            {
                await Task.Delay(config.HeartbeatPollingInterval, ct);
                continue;
            }

            var locker = _plcStateManager.PlcLocks[config.Code];
            await locker.WaitAsync(ct);

            try
            {
                var trigger = module.Trigger;
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
                    var key = $"{config.Code}:{module.TableName}";
                    var timestamp = DateTime.Now;
                    var dataMessage = new DataMessage(timestamp, module.TableName, module.BatchSize, module.DataPoints, operation);
                    if (operation == DataOperation.Insert)
                    {
                        var batchData = await client.ReadAsync(module.BatchReadRegister, module.BatchReadLength);
                        var buffer = batchData.Content;
                        if (module.DataPoints != null)
                        {
                            foreach (var dataPoint in module.DataPoints)
                            {
                                var value = TransValue(client, buffer, dataPoint.Index, dataPoint.StringByteLength, dataPoint.DataType, dataPoint.Encoding);
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
                    await _events.ErrorAsync(module.ChamberCode, $"[{module.ChamberCode}:{module.TableName}]采集异常: {ex.Message}", ex);
                }

                prevVal = currVal;
            }
            finally
            {
                locker.Release();
            }
        }
    }

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

    private static async Task<object> ReadPlcValueAsync(IPlcClientService client, string register, string dataType)
    {
        return dataType switch
        {
            "ushort" => await client.ReadUShortAsync(register),
            "uint" => await client.ReadUIntAsync(register),
            "ulong" => await client.ReadULongAsync(register),
            "short" => await client.ReadShortAsync(register),
            "int" => await client.ReadIntAsync(register),
            "long" => await client.ReadLongAsync(register),
            "float" => await client.ReadFloatAsync(register),
            "double" => await client.ReadDoubleAsync(register),
            _ => throw new NotSupportedException($"不支持的数据类型: {dataType}")
        };
    }

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
