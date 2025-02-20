using System.Net.NetworkInformation;
using DataAcquisition.Models;
using DataAcquisition.Services.PlcClients;
using HslCommunication.Profinet.Melsec;

namespace WebAppSamples.Services.PlcClients;

/// <summary>
/// PLC 客户端实现
/// </summary>
public class PlcClient : IPlcClient
{
    private readonly MelsecA1ENet _plcClient;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    public PlcClient(DataAcquisitionConfig config)
    {
        _plcClient = new MelsecA1ENet(config.Plc.IpAddress, config.Plc.Port);
        _plcClient.ReceiveTimeOut = 2000;
        _plcClient.ConnectTimeOut = 2000;
    }
    
    public async Task<OperationResult<bool>> ConnectServerAsync()
    {
        await _connectLock.WaitAsync();
        try
        {
            var result = await _plcClient.ConnectServerAsync();
            return new OperationResult<bool>()
            {
                IsSuccess = result.IsSuccess,
                Message = result.Message
            };
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task<OperationResult<bool>> ConnectCloseAsync()
    {
        await _connectLock.WaitAsync();
        try
        {
            var result = await _plcClient.ConnectCloseAsync();
            return new OperationResult<bool>()
            {
                IsSuccess = result.IsSuccess,
                Message = result.Message
            };
        }
        finally
        {
            _connectLock.Release();
        }
    }
    
    public async Task<OperationResult<bool>> IpAddressPingAsync()
    {
        await _connectLock.WaitAsync();
        try
        {
            var isSuccess = await Task.Run(()=>_plcClient.IpAddressPing() == IPStatus.Success);
            return new OperationResult<bool>()
            { 
                IsSuccess = isSuccess
            };
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task<OperationResult<UInt16>> ReadUInt16Async(string address)
    {
        var result = await _plcClient.ReadUInt16Async(address);
        return new OperationResult<UInt16>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }
    
    public async Task<OperationResult<UInt32>> ReadUInt32Async(string address)
    {
        var result = await _plcClient.ReadUInt32Async(address);
        return new OperationResult<UInt32>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }
    
    public async Task<OperationResult<UInt64>> ReadUInt64Async(string address)
    {
        var result = await _plcClient.ReadUInt64Async(address);
        return new OperationResult<UInt64>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }

    public async Task<OperationResult<Int16>> ReadInt16Async(string address)
    {
        var result = await _plcClient.ReadInt16Async(address);
        return new OperationResult<Int16>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }
    
    public async Task<OperationResult<Int32>> ReadInt32Async(string address)
    {
        var result = await _plcClient.ReadInt32Async(address);
        return new OperationResult<Int32>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }
    
    public async Task<OperationResult<Int64>> ReadInt64Async(string address)
    {
        var result = await _plcClient.ReadInt64Async(address);
        return new OperationResult<Int64>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }

    public async Task<OperationResult<float>> ReadFloatAsync(string address)
    {
        var result = await _plcClient.ReadFloatAsync(address);
        return new OperationResult<float>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }

    public async Task<OperationResult<double>> ReadDoubleAsync(string address)
    {
        var result = await _plcClient.ReadDoubleAsync(address);
        return new OperationResult<double>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }
    
    public async Task<OperationResult<string>> ReadStringAsync(string address, ushort length)
    {
        var result = await _plcClient.ReadStringAsync(address, length);
        if (result.IsSuccess)
        {
            result.Content = ParseStringValue(result.Content);
        }

        return new OperationResult<string>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }

    public async Task<OperationResult<bool>> ReadBoolAsync(string address)
    {
        var result = await _plcClient.ReadBoolAsync(address);
        return new OperationResult<bool>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
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
}