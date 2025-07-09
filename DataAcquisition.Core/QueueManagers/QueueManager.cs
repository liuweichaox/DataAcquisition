using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Utils;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace DataAcquisition.Core.QueueManagers;

/// <summary>
/// 消息队列实现
/// </summary>
public class QueueManager: AbstractQueueManager
{
    private readonly IDataStorage _dataStorage;
    private readonly IMemoryCache _memoryCache;
    private readonly IDataProcessingService _dataProcessingService;
    private readonly IMessageService _messageService;
    private readonly BlockingCollection<DataMessage> _queue = new();
    private readonly ConcurrentDictionary<string, List<DataMessage>> _dataBatchMap = new();
    public QueueManager(IDataStorage dataStorage, IMemoryCache memoryCache, IDataProcessingService dataProcessingService, IMessageService messageService)
    {
        _dataStorage = dataStorage;
        _memoryCache = memoryCache;
        _dataProcessingService = dataProcessingService;
        _messageService = messageService;
    }

    public override void EnqueueData(DataMessage dataMessage)
    {
        _queue.Add(dataMessage);
    }
    protected override async Task ProcessQueueAsync()
    {
        foreach (var dataMessage in _queue.GetConsumingEnumerable())
        {
            try
            {
                if (!IsNonZeroData(dataMessage))
                {
                    continue;
                }

                if (dataMessage.DataMessageType != DataMessageType.NewBatch && dataMessage.DataMessageType != DataMessageType.UpdateBatch)
                {
                    await _dataProcessingService.ExecuteAsync(dataMessage);
                }
                
                await StoreDataPointAsync(dataMessage);
            }
            catch (Exception ex)
            {
                await _messageService.SendAsync($"{ex.Message} - StackTrace: {ex.StackTrace}");
            }
        }

        foreach (var kv in _dataBatchMap)
        {
            if (kv.Value.Any())
            {
                await _dataStorage.SaveBatchAsync(kv.Value);
            }
        }
    }
    
    private async Task StoreDataPointAsync(DataMessage dataMessage)
    {
        if (dataMessage.DataMessageType == DataMessageType.Sensor)
        {
            var dataBatch = _dataBatchMap.GetOrAdd(dataMessage.TableName, _ => []);
            dataBatch.Add(dataMessage);

            if (dataBatch.Count >= 10)
            {
                await _dataStorage.SaveBatchAsync(dataBatch);
                await _messageService.SendAsync($"{dataMessage.TableName} 新增 10 条实时数据");
                _dataBatchMap[dataMessage.TableName] = [];
            }
        }
        else if (dataMessage.DataMessageType == DataMessageType.Recipe)
        {
            await _messageService.SendAsync($"新配方: {JsonConvert.SerializeObject(new 
            {
                dataMessage.TableName,
                dataMessage.Values
            }, Formatting.Indented)}");
            
            await _dataStorage.SaveAsync(dataMessage);
            
        } else if (dataMessage.DataMessageType == DataMessageType.NewBatch)
        {
            await _messageService.SendAsync($"新批次: {JsonConvert.SerializeObject(new 
            {
                dataMessage.TableName,
                dataMessage.Values
            }, Formatting.Indented)}");
            
            await _dataStorage.SaveAsync(dataMessage);
            
        } else if (dataMessage.DataMessageType == DataMessageType.UpdateBatch)
        {
            await _messageService.SendAsync($"更新批次: {JsonConvert.SerializeObject(new 
            {
                dataMessage.TableName,
                dataMessage.Values
            }, Formatting.Indented)}");

            var sql = $"UPDATE {dataMessage.TableName} SET end_time = @end_time WHERE batch_sequence = @batch_sequence";
            var param = new
            {
                end_time = dataMessage.Values["end_time"],
                batch_sequence = dataMessage.Values["batch_sequence"]
            };
            await _dataStorage.ExecuteAsync(sql, param);
        } 
    }
    
    private static bool IsNonZeroData(DataMessage message)
    {
        return !message.Values.All(x => x.Value == null || (DataTypeUtils.IsNumberType(x.Value) && x.Value == 0));
    }
    
    public override void Dispose()
    {
        _queue.CompleteAdding();
    }
}