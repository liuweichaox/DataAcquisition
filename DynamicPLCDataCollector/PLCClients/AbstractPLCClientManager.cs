using System.Collections.Concurrent;
using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.PLCClients;

public abstract class AbstractPLCClientManager: IPLCClientManager
{
    protected readonly ConcurrentDictionary<string, IPLClient> PLCClients = new();

    public AbstractPLCClientManager(List<Device> devices)
    {
        foreach (var device in devices)
        {
            var result = CreatePLCClient(device);
            if (result.IsSuccess)
            {
                AddPLClient(device, result.Content);
            }
        }
    }

    protected abstract OperationResult<IPLClient> CreatePLCClient(Device device);

    private void AddPLClient(Device device, IPLClient plcClient)
    {
        PLCClients[device.Code] = plcClient;
    }
    
    public async Task<Dictionary<string, object>> ReadAsync(Device device, MetricTableConfig metricTableConfig)
    {
        if (!PLCClients.TryGetValue(device.Code, out var plcClient) || !plcClient.IsConnected())
        {
            // 尝试重新连接
            if (await ReconnectAsync(device))
            {
                plcClient = PLCClients[device.Code];
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
        if (PLCClients.TryGetValue(device.Code, out var plcClient))
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
        }

        return false;
    }

    private async Task<object> ParseValue(IPLClient plcClient, string dataAddress, ushort dataLength, string dataType)
    {
        return dataType.ToLower() switch
        {
            "int" => (await RetryOnFailure(() => plcClient.ReadInt32Async(dataAddress, dataLength))).Content[0],
            "float" => (await RetryOnFailure(() => plcClient.ReadFloatAsync(dataAddress, dataLength))).Content[0],
            "double" => (await RetryOnFailure(() => plcClient.ReadDoubleAsync(dataAddress, dataLength))).Content[0],
            "string" => (await RetryOnFailure(() => plcClient.ReadStringAsync(dataAddress, dataLength))).Content,
            "boolean" => (await RetryOnFailure(() => plcClient.ReadBoolAsync(dataAddress, dataLength))).Content[0],
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
        foreach (var client  in PLCClients.Values)
        {
           await client.ConnectCloseAsync();
        }
    }
}