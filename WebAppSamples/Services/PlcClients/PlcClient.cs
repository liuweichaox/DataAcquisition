using System.Net.NetworkInformation;
using System.Text;
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

    public PlcClient(DataAcquisitionConfig config)
    {
        _plcClient = new MelsecA1ENet(config.Plc.IpAddress, config.Plc.Port);
        _plcClient.ReceiveTimeOut = 2000;
        _plcClient.ConnectTimeOut = 2000;
    }

    public async Task<OperationResult<bool>> ConnectServerAsync()
    {
        var result = await _plcClient.ConnectServerAsync();
        return new OperationResult<bool>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message
        };
    }

    public async Task<OperationResult<bool>> ConnectCloseAsync()
    {
        var result = await _plcClient.ConnectCloseAsync();
        return new OperationResult<bool>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message
        };
    }

    public async Task<OperationResult<bool>> IpAddressPingAsync()
    {
        var isSuccess = await Task.Run(() => _plcClient.IpAddressPing() == IPStatus.Success);
        return new OperationResult<bool>()
        {
            IsSuccess = isSuccess
        };
    }

    public async Task<OperationResult<byte[]>> ReadAsync(string address, ushort length)
    {
        var result = await _plcClient.ReadAsync(address, length);
        return new OperationResult<byte[]>()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            Content = result.Content
        };
    }

    public ushort TransUInt16(byte[] buffer, int index)
    {
        return _plcClient.ByteTransform.TransUInt16(buffer, index);
    }

    public uint TransUInt32(byte[] buffer, int index)
    {
        return _plcClient.ByteTransform.TransUInt32(buffer, index);
    }

    public ulong TransUInt64(byte[] buffer, int index)
    {
        return _plcClient.ByteTransform.TransUInt64(buffer, index);
    }

    public short TransInt16(byte[] buffer, int index)
    {
        return _plcClient.ByteTransform.TransInt16(buffer, index);
    }

    public int TransInt32(byte[] buffer, int index)
    {
        return _plcClient.ByteTransform.TransInt32(buffer, index);
    }

    public long TransInt64(byte[] buffer, int index)
    {
        return _plcClient.ByteTransform.TransInt64(buffer, index);
    }

    public float TransSingle(byte[] buffer, int index)
    {
        return _plcClient.ByteTransform.TransSingle(buffer, index);
    }

    public double TransDouble(byte[] buffer, int index)
    {
        return _plcClient.ByteTransform.TransDouble(buffer, index);
    }

    public string TransString(byte[] buffer, int index, int length, string encoding)
    {
        var encodingObj = Encoding.GetEncoding(encoding);
        return _plcClient.ByteTransform.TransString(buffer, index, length, encodingObj);
    }

    public bool TransBool(byte[] buffer, int index)
    {
        return _plcClient.ByteTransform.TransBool(buffer, index);
    }
}