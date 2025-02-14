using System.Collections.Concurrent;
using DataAcquisition.Common;
using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;
using DataAcquisition.Services.QueueManagers;

namespace Samples.Services.QueueManagers;

/// <summary>
/// 消息队列里实现
/// </summary>
public class QueueManager : AbstractQueueManager
{
    private readonly BlockingCollection<Dictionary<string, object>> _queue;
    private readonly IDataStorage _dataStorage;
    private readonly List<Dictionary<string, object>> _dataBatch;
    private readonly DataAcquisitionConfig _dataAcquisitionConfig;

    public QueueManager(DataStorageFactory dataStorageFactory, DataAcquisitionConfig dataAcquisitionConfig) : base(
        dataStorageFactory, dataAcquisitionConfig)
    {
        _queue = new BlockingCollection<Dictionary<string, object>>();
        _dataStorage = dataStorageFactory(dataAcquisitionConfig);
        _dataAcquisitionConfig = dataAcquisitionConfig;
        _dataBatch = new List<Dictionary<string, object>>();
    }

    public override void EnqueueData(Dictionary<string, object> data)
    {
        _queue.Add(data);
    }
    public override async Task ProcessQueueAsync()
    {
        foreach (var data in _queue.GetConsumingEnumerable())
        {
            _dataBatch.Add(data);

            if (_dataBatch.Count >= _dataAcquisitionConfig.BatchSize)
            {
                await _dataStorage.SaveBatchAsync(_dataBatch);
                _dataBatch.Clear();
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