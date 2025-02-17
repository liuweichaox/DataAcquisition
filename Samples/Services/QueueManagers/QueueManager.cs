using System.Collections.Concurrent;
using DataAcquisition.Common;
using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.QueueManagers;

namespace Samples.Services.QueueManagers;

/// <summary>
/// 消息队列里实现
/// </summary>
public class QueueManager(DataStorageFactory dataStorageFactory, DataAcquisitionConfig dataAcquisitionConfig)
    : AbstractQueueManager(dataStorageFactory, dataAcquisitionConfig)
{
    private readonly BlockingCollection<DataPoint?> _queue = new();
    private readonly IDataStorage _dataStorage = dataStorageFactory(dataAcquisitionConfig);
    private readonly List<DataPoint?> _dataBatch = [];
    private readonly DataAcquisitionConfig _dataAcquisitionConfig = dataAcquisitionConfig;

    public override void EnqueueData(DataPoint? dataPoint)
    {
        _queue.Add(dataPoint);
    }
    public override async Task ProcessQueueAsync()
    {
        foreach (var data in _queue.GetConsumingEnumerable())
        {
            if (_dataAcquisitionConfig.BatchSize > 1)
            {
                _dataBatch.Add(data);
            
                if (_dataBatch.Count >= _dataAcquisitionConfig.BatchSize)
                {
                    await _dataStorage.SaveBatchAsync(_dataBatch);
                    _dataBatch.Clear();
                }
            }
            else
            {
                await _dataStorage.SaveAsync(data);
            }
        }

        if (_dataBatch.Count > 0)
        {
            await _dataStorage.SaveBatchAsync(_dataBatch);
        }
    }

    public override void Complete()
    {
        _queue.CompleteAdding();
        _dataStorage.DisposeAsync();
    }
}