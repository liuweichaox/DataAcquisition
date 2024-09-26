using System.Collections.Concurrent;
using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.PLCClients;

/// <summary>
/// PLC 连接管理器抽象类
/// </summary>
public abstract class AbstractPLCClientManager: IPLCClientManager
{
    private readonly ConcurrentDictionary<string, IPLClient> _plcClients;

    public AbstractPLCClientManager()
    {
        _plcClients = new ConcurrentDictionary<string, IPLClient>();
    }

    protected abstract OperationResult<IPLClient> CreatePLCClient(Device device);

    private void AddPLClient(Device device, IPLClient plcClient)
    {
        _plcClients[device.Code] = plcClient;
    }
    
    public async Task<Dictionary<string, object>> ReadAsync(Device device, MetricTableConfig metricTableConfig)
    {
        if (!_plcClients.TryGetValue(device.Code, out var plcClient) || !plcClient.IsConnected())
        {
            // 尝试重新连接
            if (await ReconnectAsync(device))
            {
                plcClient = _plcClients[device.Code];
            }
            else
            {
                throw new Exception($"连接设备 {device.Code} 失败");
            }
        }

        var data = new Dictionary<string, object>
        {
            { "TimeStamp", DateTime.Now },
            { "Device", device.Code }
        };

        foreach (var metricColumnConfig in metricTableConfig.MetricColumnConfigs)
        {
            try
            {
                data[metricColumnConfig.ColumnName] = await ParseValue(plcClient, metricColumnConfig.DataAddress, metricColumnConfig.DataLength, metricColumnConfig.DataType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 读取设备 {device.Code} 失败：{ex.Message}");
            }
        }

        return data;
    }
    
    private async Task<bool> ReconnectAsync(Device device)
    {
        if (_plcClients.TryGetValue(device.Code, out var plcClient))
        {
            for (var i = 0; i < 5; i++)  // 尝试重连5次
            {
                var connect = await plcClient.ConnectServerAsync();
                if (connect.IsSuccess)
                {
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 重新连接到设备 {device.Code} 成功！");
                    return true;
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 重新连接到设备 {device.Code} 失败：{connect.Message}");
                    await Task.Delay(2000);  // 等待2秒后再次尝试
                }
            }
        }
        else
        {
            var result = CreatePLCClient(device);
            if (result.IsSuccess)
            {
                AddPLClient(device, result.Content);
            }

            return result.IsSuccess;
        }

        return false;
    }

    private async Task<object> ParseValue(IPLClient plcClient, string dataAddress, ushort dataLength, string dataType)
    {
        return dataType.ToLower() switch
        {
            "int" => (await RetryOnFailure(() => plcClient.ReadInt32Async(dataAddress))).Content,
            "float" => (await RetryOnFailure(() => plcClient.ReadFloatAsync(dataAddress))).Content,
            "double" => (await RetryOnFailure(() => plcClient.ReadDoubleAsync(dataAddress))).Content,
            "string" => (await RetryOnFailure(() => plcClient.ReadStringAsync(dataAddress, dataLength))).Content,
            "boolean" => (await RetryOnFailure(() => plcClient.ReadBoolAsync(dataAddress))).Content,
            _ => throw new ArgumentException("未知的数据类型")
        };
    }
    
    private async Task<OperationResult<T>> RetryOnFailure<T>(Func<Task<OperationResult<T>>> action, int maxRetries = 3)
    {
        var retries = 0;
        while (retries < maxRetries)
        {
            var result = await action();
            if (result.IsSuccess)
            {
                return result;
            }
            retries++;
            await Task.Delay(1000);  // 等待1秒后重试
        }
        throw new Exception($"操作失败，已达到最大重试次数 {maxRetries}。");
    }
    
    public async Task DisconnectAllAsync()
    {
        foreach (var client  in _plcClients.Values)
        {
           await client.ConnectCloseAsync();
        }
    }
}