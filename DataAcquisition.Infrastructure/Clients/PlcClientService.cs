using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using HslCommunication.Core.Device;
using HslCommunication.Profinet.Melsec;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
/// 基于 HslCommunication 的 PLC 通讯客户端
/// </summary>
public class PlcClientService : IPlcClientService
{
    private readonly DeviceTcpNet _device;

    public PlcClientService(DeviceConfig config)
    {
        _device = new MelsecA1ENet(config.Host, config.Port)
        {
            ReceiveTimeOut = 2000,
            ConnectTimeOut = 2000
        };
    }

    public Task ConnectCloseAsync() => _device.ConnectCloseAsync();

    public IPStatus IpAddressPing() => _device.IpAddressPing();
    public async Task<PlcWriteResult> WriteUShortAsync(string address, ushort value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcWriteResult> WriteUIntAsync(string address, uint value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcWriteResult> WriteULongAsync(string address, ulong value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcWriteResult> WriteShortAsync(string address, short value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcWriteResult> WriteIntAsync(string address, int value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcWriteResult> WriteLongAsync(string address, long value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcWriteResult> WriteFloatAsync(string address, float value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcWriteResult> WriteDoubleAsync(string address, double value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcWriteResult> WriteStringAsync(string address, string value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcWriteResult> WriteBoolAsync(string address, bool value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public async Task<PlcReadResult> ReadAsync(string address, ushort length)
    {
        var res = await _device.ReadAsync(address, length);
        return new PlcReadResult
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
