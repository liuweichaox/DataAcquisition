using System.Net.NetworkInformation;
using DataAcquisition.Models;
using DataAcquisition.Services.PLCClients;
using HslCommunication.Profinet.Inovance;

namespace Samples.Services.PLCClients;

/// <summary>
/// PLC 客户端实现
/// </summary>
public class PLCClient : IPLCClient
{
    private readonly InovanceTcpNet _plcClient;
    public PLCClient(string ipAddress, int port)
    {   
        _plcClient = new InovanceTcpNet(ipAddress, port)
        {
            Station = 1,
            AddressStartWithZero = true,
            IsStringReverse = true,
            ConnectTimeOut = 1000
        };
    }

    public async Task<OperationResult<bool>> ConnectServerAsync()
    {
        var result = await _plcClient.ConnectServerAsync();
        return result.IsSuccess 
            ? new OperationResult<bool>(true) 
            : new OperationResult<bool>(result.Message);
    }

    public async Task<OperationResult<bool>> ConnectCloseAsync()
    {
        var result = await _plcClient.ConnectCloseAsync();
        return result.IsSuccess 
            ? new OperationResult<bool>(true) 
            : new OperationResult<bool>(result.Message);
    }

    public bool IsConnected()
    {
        return _plcClient.IpAddressPing() == IPStatus.Success;
    }

    public async Task<OperationResult<int>> ReadInt32Async(string address)
    {
        var result = await _plcClient.ReadInt32Async(address);
        return result.IsSuccess 
            ? new OperationResult<int>(result.Content) 
            : new OperationResult<int>(result.Message);
    }

    public async Task<OperationResult<float>> ReadFloatAsync(string address)
    {
        var result = await _plcClient.ReadFloatAsync(address);
        return result.IsSuccess 
            ? new OperationResult<float>(result.Content) 
            : new OperationResult<float>(result.Message);
    }

    public async Task<OperationResult<double>> ReadDoubleAsync(string address)
    {
        var result = await _plcClient.ReadDoubleAsync(address);
        return result.IsSuccess 
            ? new OperationResult<double>(result.Content) 
            : new OperationResult<double>(result.Message);
    }

    public async Task<OperationResult<string>> ReadStringAsync(string address, ushort length)
    {
        var result = await _plcClient.ReadStringAsync(address, length);
        return result.IsSuccess 
            ? new OperationResult<string>(ParseStringValue(result.Content)) 
            : new OperationResult<string>(result.Message);
    }

    public async Task<OperationResult<bool>> ReadBoolAsync(string address)
    {
        var result = await _plcClient.ReadBoolAsync(address);
        return result.IsSuccess 
            ? new OperationResult<bool>(result.Content) 
            : new OperationResult<bool>(result.Message);
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
}