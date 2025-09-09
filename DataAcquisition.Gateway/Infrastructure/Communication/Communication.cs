using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using DataAcquisition.Core.Communication;
using DataAcquisition.Core.Models;
using HslCommunication.Core.Device;
using HslCommunication.Profinet.Melsec;

namespace DataAcquisition.Gateway.Infrastructure.Communication;

/// <summary>
/// 基于 HslCommunication 的通讯客户端适配器
/// </summary>
public class Communication : ICommunication
{
    private readonly DeviceTcpNet _device;

    public Communication(DeviceConfig config)
    {
        _device = new MelsecA1ENet(config.Host, config.Port)
        {
            ReceiveTimeOut = 2000,
            ConnectTimeOut = 2000
        };
    }

    public Task ConnectCloseAsync() => _device.ConnectCloseAsync();

    public IPStatus IpAddressPing() => _device.IpAddressPing();
    public async Task<CommunicationWriteResult> WriteUShortAsync(string address, ushort value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationWriteResult> WriteUIntAsync(string address, uint value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationWriteResult> WriteULongAsync(string address, ulong value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationWriteResult> WriteShortAsync(string address, short value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationWriteResult> WriteIntAsync(string address, int value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationWriteResult> WriteLongAsync(string address, long value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationWriteResult> WriteFloatAsync(string address, float value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationWriteResult> WriteDoubleAsync(string address, double value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationWriteResult> WriteStringAsync(string address, string value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationWriteResult> WriteBoolAsync(string address, bool value)
    {
        var res = await _device.WriteAsync(address, value);
        return new CommunicationWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<CommunicationReadResult> ReadAsync(string address, ushort length)
    {
        var res = await _device.ReadAsync(address, length);
        return new CommunicationReadResult
        {
            IsSuccess = res.IsSuccess,
            Content = res.Content ?? System.Array.Empty<byte>(),
            Message = res.Message
        };
    }

    public async Task<ushort> ReadUShortAsync(string address)
    {
        var res = await _device.ReadUInt16Async(address, 1);
        return res.Content[0];
    }

    public async Task<uint> ReadUIntAsync(string address)
    {
        var res = await _device.ReadUInt32Async(address, 1);
        return res.Content[0];
    }

    public async Task<ulong> ReadULongAsync(string address)
    {
        var res = await _device.ReadUInt64Async(address, 1);
        return res.Content[0];
    }

    public async Task<short> ReadShortAsync(string address)
    {
        var res = await _device.ReadInt16Async(address, 1);
        return res.Content[0];
    }

    public async Task<int> ReadIntAsync(string address)
    {
        var res = await _device.ReadInt32Async(address, 1);
        return res.Content[0];
    }

    public async Task<long> ReadLongAsync(string address)
    {
        var res = await _device.ReadInt64Async(address, 1);
        return res.Content[0];
    }

    public async Task<float> ReadFloatAsync(string address)
    {
        var res = await _device.ReadFloatAsync(address, 1);
        return res.Content[0];
    }

    public async Task<double> ReadDoubleAsync(string address)
    {
        var res = await _device.ReadDoubleAsync(address, 1);
        return res.Content[0];
    }

    public ushort TransUShort(byte[] buffer, int index) => _device.ByteTransform.TransUInt16(buffer, index);
    public uint TransUInt(byte[] buffer, int index) => _device.ByteTransform.TransUInt32(buffer, index);
    public ulong TransULong(byte[] buffer, int index) => _device.ByteTransform.TransUInt64(buffer, index);
    public short TransShort(byte[] buffer, int index) => _device.ByteTransform.TransInt16(buffer, index);
    public int TransInt(byte[] buffer, int index) => _device.ByteTransform.TransInt32(buffer, index);
    public long TransLong(byte[] buffer, int index) => _device.ByteTransform.TransInt64(buffer, index);
    public float TransFloat(byte[] buffer, int index) => _device.ByteTransform.TransSingle(buffer, index);
    public double TransDouble(byte[] buffer, int index) => _device.ByteTransform.TransDouble(buffer, index);
    public string TransString(byte[] buffer, int index, int length, Encoding encoding) => _device.ByteTransform.TransString(buffer, index, length, encoding);
    public bool TransBool(byte[] buffer, int index) => _device.ByteTransform.TransBool(buffer, index);
}
