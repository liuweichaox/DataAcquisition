using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Delegates;
using NCalc;

namespace DataAcquisition.Core.QueueManagers;

/// <summary>
/// 消息队列里实现
/// </summary>
public class QueueManager(
    IDataStorage dataStorage,
    DataAcquisitionConfig dataAcquisitionConfig,
    MessageSendDelegate messageSendDelegate)
    : AbstractQueueManager(dataStorage, dataAcquisitionConfig, messageSendDelegate)
{
    private readonly BlockingCollection<DataPoint?> _queue = new();
    private readonly List<DataPoint> _dataBatch = [];
    private readonly DataAcquisitionConfig _dataAcquisitionConfig = dataAcquisitionConfig;
    private readonly IDataStorage _dataStorage = dataStorage;

    public override void EnqueueData(DataPoint dataPoint)
    {
        _queue.Add(dataPoint);
    }

    protected override async Task ProcessQueueAsync()
    {
        foreach (var dataPoint in _queue.GetConsumingEnumerable())
        {
            try
            {
                var preprocessData = await PreprocessAsync(dataPoint);

                await StoreDataPointAsync(preprocessData);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        if (_dataBatch.Count > 0)
        {
            await _dataStorage.SaveBatchAsync(_dataBatch);
        }
    }
    private async Task StoreDataPointAsync(DataPoint preprocessData)
    {
        var config = _dataAcquisitionConfig.Plc.RegisterGroups.SingleOrDefault(x => x.TableName == preprocessData.TableName);
        if (config.BatchSize > 1)
        {
            _dataBatch.Add(preprocessData);

            if (_dataBatch.Count >= config.BatchSize)
            {
                await _dataStorage.SaveBatchAsync(_dataBatch);
                _dataBatch.Clear();
            }
        }
        else
        {
            await _dataStorage.SaveAsync(preprocessData);
        }
    }

    private async Task<DataPoint> PreprocessAsync(DataPoint dataPoint)
    {
        dataPoint = await EvalExpressionAsync(dataPoint);

        return dataPoint;
    }

    private bool IsNumberType(object value)
    {
        return value is ushort or uint or ulong or short or int or long or float or double;
    }
    
    private async Task<DataPoint> EvalExpressionAsync(DataPoint dataPoint)
    {
        var config = _dataAcquisitionConfig.Plc.RegisterGroups.SingleOrDefault(x => x.TableName == dataPoint.TableName);
        foreach (var kv in dataPoint.Values)
        {
            if (!IsNumberType(kv.Value)) continue;
            var register = config?.Registers.SingleOrDefault(x => x.ColumnName == kv.Key);
            if (register == null || string.IsNullOrWhiteSpace(register.EvalExpression) || kv.Value == null) continue;
            var expression = new AsyncExpression(register.EvalExpression)
            {
                Parameters =
                {
                    ["value"] = kv.Value
                }
            };

            var value = await expression.EvaluateAsync();
            dataPoint.Values[kv.Key] = value ?? 0;
        }

        return dataPoint;
    }
    public override void Complete()
    {
        _queue.CompleteAdding();
        _dataStorage.Dispose();
    }
}