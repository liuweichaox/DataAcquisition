using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using HslCommunication.Core.Device;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 基于 HslCommunication 的 PLC 客户端适配器
/// </summary>
public class HslPlcClient : IPlcClient
{
    private readonly DeviceTcpNet _device;

    public HslPlcClient(DeviceTcpNet device)
    {
        _device = device;
    }

    public Task ConnectCloseAsync() => _device.ConnectCloseAsync();

    public IPStatus IpAddressPing() => _device.IpAddressPing();

    public async Task<PlcWriteResult> WriteAsync(string address, int value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public PlcReadResult Read(string address, ushort length)
    {
        var res = _device.Read(address, length);
        return new PlcReadResult
        {
            IsSuccess = res.IsSuccess,
            Content = res.Content ?? System.Array.Empty<byte>(),
            Message = res.Message
        };
    }

    public ushort ReadUInt16(string address)
    {
        var res = _device.ReadUInt16(address, 1);
        return res.Content[0];
    }

    public uint ReadUInt32(string address)
    {
        var res = _device.ReadUInt32(address, 1);
        return res.Content[0];
    }

    public ulong ReadUInt64(string address)
    {
        var res = _device.ReadUInt64(address, 1);
        return res.Content[0];
    }

    public short ReadInt16(string address)
    {
        var res = _device.ReadInt16(address, 1);
        return res.Content[0];
    }

    public int ReadInt32(string address)
    {
        var res = _device.ReadInt32(address, 1);
        return res.Content[0];
    }

    public long ReadInt64(string address)
    {
        var res = _device.ReadInt64(address, 1);
        return res.Content[0];
    }

    public float ReadFloat(string address)
    {
        var res = _device.ReadFloat(address, 1);
        return res.Content[0];
    }

    public double ReadDouble(string address)
    {
        var res = _device.ReadDouble(address, 1);
        return res.Content[0];
    }

    public ushort TransUInt16(byte[] buffer, int index) => _device.ByteTransform.TransUInt16(buffer, index);
    public uint TransUInt32(byte[] buffer, int index) => _device.ByteTransform.TransUInt32(buffer, index);
    public ulong TransUInt64(byte[] buffer, int index) => _device.ByteTransform.TransUInt64(buffer, index);
    public short TransInt16(byte[] buffer, int index) => _device.ByteTransform.TransInt16(buffer, index);
    public int TransInt32(byte[] buffer, int index) => _device.ByteTransform.TransInt32(buffer, index);
    public long TransInt64(byte[] buffer, int index) => _device.ByteTransform.TransInt64(buffer, index);
    public float TransSingle(byte[] buffer, int index) => _device.ByteTransform.TransSingle(buffer, index);
    public double TransDouble(byte[] buffer, int index) => _device.ByteTransform.TransDouble(buffer, index);
    public string TransString(byte[] buffer, int index, int length, Encoding encoding) => _device.ByteTransform.TransString(buffer, index, length, encoding);
    public bool TransBool(byte[] buffer, int index) => _device.ByteTransform.TransBool(buffer, index);
}
