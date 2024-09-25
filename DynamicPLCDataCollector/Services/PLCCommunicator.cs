using System.Collections.Concurrent;
using DynamicPLCDataCollector.Models;
using HslCommunication;
using HslCommunication.Profinet.Inovance;

namespace DynamicPLCDataCollector.Services;


public class PLCCommunicator : IPLCCommunicator
{
    private static readonly ConcurrentDictionary<string, InovanceTcpNet> PLCClients = new();

    public PLCCommunicator(List<Device> devices)
    {
        foreach (var device in devices)
        {
            var plcClient = new InovanceTcpNet(device.IpAddress, device.Port)
            {
                Station = 1,
                AddressStartWithZero = true,
                IsStringReverse = true,
                ConnectTimeOut = 1000
            };
            var connect = plcClient.ConnectServer();
            if (connect.IsSuccess)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {device.Code} 成功！");
                PLCClients[device.Code] = plcClient;
            }
            else
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {device.Code} 失败：{connect.Message}");
            }
        }
    }

    public async Task<Dictionary<string, object>> ReadAsync(Device device, MetricTableConfig metricTableConfig)
    {
        if (!PLCClients.TryGetValue(device.Code, out var plcClient) || plcClient.IpAddressPing() != System.Net.NetworkInformation.IPStatus.Success)
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
                data[metricColumnConfig.ColumnName] = await ParseValue(plcClient, metricColumnConfig);
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
        return false;
    }

    private async Task<OperateResult<T>> RetryOnFailure<T>(Func<Task<OperateResult<T>>> action, int maxRetries = 3)
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

    private async Task<object> ParseValue(InovanceTcpNet plcClient, MetricColumnConfig metricColumnConfig)
    {
        return metricColumnConfig.DataType.ToLower() switch
        {
            "int" => (await RetryOnFailure(() => plcClient.ReadInt32Async(metricColumnConfig.DataAddress, metricColumnConfig.DataLength))).Content[0],
            "float" => (await RetryOnFailure(() => plcClient.ReadFloatAsync(metricColumnConfig.DataAddress, metricColumnConfig.DataLength))).Content[0],
            "double" => (await RetryOnFailure(() => plcClient.ReadDoubleAsync(metricColumnConfig.DataAddress, metricColumnConfig.DataLength))).Content[0],
            "string" => ParseStringValue((await RetryOnFailure(() => plcClient.ReadStringAsync(metricColumnConfig.DataAddress, metricColumnConfig.DataLength))).Content),
            "boolean" => (await RetryOnFailure(() => plcClient.ReadBoolAsync(metricColumnConfig.DataAddress, metricColumnConfig.DataLength))).Content[0],
            _ => throw new ArgumentException("未知的数据类型")
        };
    }

    private string ParseStringValue(string stringValue)
    {
        // 查找终止符
        var nullCharIndex = stringValue.IndexOf('\0');
        if (nullCharIndex >= 0)
        {
            // 如果找到终止符，则截断字符串
            stringValue = stringValue.Substring(0, nullCharIndex);
        }
        return stringValue;
    }

    public async Task DisconnectAllAsync()
    {
        foreach (var client in PLCClients.Values)
        {
            await client.ConnectCloseAsync();
        }
    }
}