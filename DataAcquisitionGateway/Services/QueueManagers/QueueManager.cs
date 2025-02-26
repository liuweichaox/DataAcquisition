using System.Collections.Concurrent;
using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.Messages;
using DataAcquisition.Services.QueueManagers;
using Microsoft.Extensions.Caching.Memory;
using NCalc;
using DataAcquisitionGateway.Hubs;

namespace DataAcquisitionGateway.Services.QueueManagers;

/// <summary>
/// 消息队列里实现
/// </summary>
public class QueueManager(
    IDataStorage dataStorage,
    DataAcquisitionConfig dataAcquisitionConfig,
    IMessageService messageService)
    : AbstractQueueManager(dataStorage, dataAcquisitionConfig, messageService)
{
    private readonly BlockingCollection<DataPoint?> _queue = new();
    private readonly List<DataPoint> _dataBatch = [];
    private readonly DataAcquisitionConfig _dataAcquisitionConfig = dataAcquisitionConfig;
    private readonly IDataStorage _dataStorage = dataStorage;
    private readonly IMemoryCache _memoryCache = ServiceLocator.GetService<IMemoryCache>();

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
        var config = _dataAcquisitionConfig.Plc.RegisterGroups.SingleOrDefault(x => x.TableName == dataPoint.TableName);
        foreach (var kv in dataPoint.Values)
        {
            var register = config?.Registers.SingleOrDefault(x => x.ColumnName == kv.Key);
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