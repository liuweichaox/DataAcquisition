using System.Collections.Concurrent;
using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.Messages;
using DataAcquisition.Services.QueueManagers;
using NCalc;

namespace WebAppSamples.Services.QueueManagers;

/// <summary>
/// 消息队列里实现
/// </summary>
public class QueueManager(IDataStorage dataStorage, DataAcquisitionConfig dataAcquisitionConfig, IMessageService messageService)
    : AbstractQueueManager(dataStorage, dataAcquisitionConfig, messageService)
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
        foreach (var data in _queue.GetConsumingEnumerable())
        {
           var preprocessData= await PreprocessAsync(data);
            
            if (_dataAcquisitionConfig.BatchSize > 1)
            {
                _dataBatch.Add(preprocessData);
            
                if (_dataBatch.Count >= _dataAcquisitionConfig.BatchSize)
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

        if (_dataBatch.Count > 0)
        {
            await _dataStorage.SaveBatchAsync(_dataBatch);
        }
    }

    private async Task<DataPoint> PreprocessAsync(DataPoint dataPoint)
    {
        var config = _dataAcquisitionConfig.Plc.RegisterGroups.SingleOrDefault(x=>x.TableName == dataPoint.TableName);
        foreach (var kv in dataPoint.Values)
        {
            var register = config?.Registers.SingleOrDefault(x=>x.ColumnName == kv.Key);
            if (register != null)
            {
                dataPoint.Values[kv.Key] = await EvaluateAsync(register, kv.Value);
            }
        }
        return dataPoint;
    }

    private async Task<object> EvaluateAsync(Register register, object content)
    {
        var types = new[] { "ushort", "uint", "ulong", "int", "long", "float", "double" };
        if (!types.Contains(register.DataType))
        {
            return content;
        }
        
        var expression = new AsyncExpression(register.EvalExpression)
        {
            Parameters =
            {
                ["value"] = content
            }
        };

        var value = await expression.EvaluateAsync();
        return value ?? 0;
    }

    public override void Complete()
    {
        _queue.CompleteAdding();
        _dataStorage.DisposeAsync();
    }
}