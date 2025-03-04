﻿using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using HslCommunication.Core.Device;

namespace DataAcquisition.Core.Communication;

public abstract class PlcDriverBase : IPlcDriver
{
    private readonly DeviceTcpNet _plcClient;

    protected PlcDriverBase()
    {
        _plcClient = CreateDeviceTcpNet();
    }
    public abstract DeviceTcpNet CreateDeviceTcpNet();
    
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

    public OperationResult<byte[]> Read(string address, ushort length)
    {
        var result = _plcClient.Read(address, length);
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

    public OperationResult WriteUInt16(string address, ushort value)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }

    public OperationResult WriteUInt32(string address, uint value)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }

    public OperationResult WriteUInt64(string address, ulong value)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }

    public OperationResult WriteInt16(string address, short value)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }

    public OperationResult WriteInt32(string address, int value)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }

    public OperationResult WriteInt64(string address, long value)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }

    public OperationResult WriteSingle(string address, float value)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }

    public OperationResult WriteDouble(string address, double value)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }

    public OperationResult WriteString(string address, string value, Encoding encoding)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }

    public OperationResult WriteBool(string address, bool value)
    {
        var result = _plcClient.Write(address, value);
        return new OperationResult()
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
        };
    }
}