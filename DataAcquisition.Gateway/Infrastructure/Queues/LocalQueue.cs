using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Channels;
using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Models;

namespace DataAcquisition.Gateway.Infrastructure.Queues;

/// <summary>
/// 消息队列实现
/// </summary>
public class LocalQueue : IQueue
{
    private readonly Channel<DataMessage> _channel = Channel.CreateUnbounded<DataMessage>();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    private readonly IDataStorage _dataStorage;
    private readonly IDataProcessingService _dataProcessingService;
    private readonly IMessage _message;

    public LocalQueue(IDataStorage dataStorage, IDataProcessingService dataProcessingService, IMessage message)
    {
        _dataStorage = dataStorage;
        _dataProcessingService = dataProcessingService;
        _message = message;
    }

    public async Task PublishAsync(DataMessage dataMessage)
    {
        await _channel.Writer.WriteAsync(dataMessage);
    }

    public async Task SubscribeAsync(CancellationToken ct)
    {
        await foreach (var dataMessage in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await _dataProcessingService.ExecuteAsync(dataMessage);
                await StoreDataPointAsync(dataMessage);
            }
            catch (Exception ex)
            { 
                await _message.SendAsync($"Error processing message: {ex.Message}");
            }
        }
    }

    private async Task StoreDataPointAsync(DataMessage dataMessage)
    {
        await Task.Yield();
        if (dataMessage.Operation == DataOperation.Update)
        {
            await _dataStorage.UpdateAsync(
                dataMessage.TableName,
                dataMessage.DataValues.ToDictionary(k => k.Key, k => (object)k.Value),
                dataMessage.KeyValues.ToDictionary(k => k.Key, k => (object)k.Value));
            return;
        }

        if (dataMessage.BatchSize <= 1)
        {
            await _dataStorage.SaveAsync(dataMessage);
            return;
        }

        var batch = _dataBatchMap.GetOrAdd(dataMessage.TableName, _ => new List<DataMessage>());
        batch.Add(dataMessage);

        if (batch.Count >= dataMessage.BatchSize)
        {
            await _dataStorage.SaveBatchAsync(batch);
            batch.Clear();
        }
    }

    public ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        return ValueTask.CompletedTask;
    }
}
