using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Clients;
using DataAcquisition.Domain.Models;
using HslCommunication.Core.Device;
using HslCommunication.Profinet.Beckhoff;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
/// 基于 HslCommunication 的 PLC 通讯客户端
/// </summary>
public class BeckhoffAdsPLCClientService(DeviceConfig config) : IPLCClientService
{
    private readonly DeviceTcpNet _device = new BeckhoffAdsNet(config.Host, config.Port)
    {
        ReceiveTimeOut = 5000, // 增加接收超时时间到 5 秒
        ConnectTimeOut = 5000  // 增加连接超时时间到 5 秒
    };

    /// <summary>
    /// 关闭与 PLC 的连接。
    /// </summary>
    public Task ConnectCloseAsync() => _device.ConnectCloseAsync();

    /// <summary>
    /// Ping 设备 IP，返回连通状态。
    /// </summary>
    public IPStatus IpAddressPing() => _device.IpAddressPing();

    /// <summary>
    /// 写入无符号短整型值。
    /// </summary>
    public async Task<PLCWriteResult> WriteUShortAsync(string address, ushort value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 写入无符号整型值。
    /// </summary>
    public async Task<PLCWriteResult> WriteUIntAsync(string address, uint value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 写入无符号长整型值。
    /// </summary>
    public async Task<PLCWriteResult> WriteULongAsync(string address, ulong value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 写入短整型值。
    /// </summary>
    public async Task<PLCWriteResult> WriteShortAsync(string address, short value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 写入整型值。
    /// </summary>
    public async Task<PLCWriteResult> WriteIntAsync(string address, int value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 写入长整型值。
    /// </summary>
    public async Task<PLCWriteResult> WriteLongAsync(string address, long value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 写入单精度浮点值。
    /// </summary>
    public async Task<PLCWriteResult> WriteFloatAsync(string address, float value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 写入双精度浮点值。
    /// </summary>
    public async Task<PLCWriteResult> WriteDoubleAsync(string address, double value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 写入字符串。
    /// </summary>
    public async Task<PLCWriteResult> WriteStringAsync(string address, string value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 写入布尔值。
    /// </summary>
    public async Task<PLCWriteResult> WriteBoolAsync(string address, bool value)
    {
        var res = await _device.WriteAsync(address, value);
        return new PLCWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    /// <summary>
    /// 批量读取原始字节数据。
    /// </summary>
    public async Task<PLCReadResult> ReadAsync(string address, ushort length)
    {
        var res = await _device.ReadAsync(address, length);
        return new PLCReadResult
        {
            IsSuccess = res.IsSuccess,
            Content = res.Content ?? System.Array.Empty<byte>(),
            Message = res.Message
        };
    }

    /// <summary>
    /// 读取无符号短整型值。
    /// </summary>
    public async Task<ushort> ReadUShortAsync(string address)
    {
        var res = await _device.ReadUInt16Async(address, 1);
        return res.Content[0];
    }

    /// <summary>
    /// 读取无符号整型值。
    /// </summary>
    public async Task<uint> ReadUIntAsync(string address)
    {
        var res = await _device.ReadUInt32Async(address, 1);
        return res.Content[0];
    }

    /// <summary>
    /// 读取无符号长整型值。
    /// </summary>
    public async Task<ulong> ReadULongAsync(string address)
    {
        var res = await _device.ReadUInt64Async(address, 1);
        return res.Content[0];
    }

    /// <summary>
    /// 读取短整型值。
    /// </summary>
    public async Task<short> ReadShortAsync(string address)
    {
        var res = await _device.ReadInt16Async(address, 1);
        return res.Content[0];
    }

    /// <summary>
    /// 读取整型值。
    /// </summary>
    public async Task<int> ReadIntAsync(string address)
    {
        var res = await _device.ReadInt32Async(address, 1);
        return res.Content[0];
    }

    /// <summary>
    /// 读取长整型值。
    /// </summary>
    public async Task<long> ReadLongAsync(string address)
    {
        var res = await _device.ReadInt64Async(address, 1);
        return res.Content[0];
    }

    /// <summary>
    /// 读取单精度浮点值。
    /// </summary>
    public async Task<float> ReadFloatAsync(string address)
    {
        var res = await _device.ReadFloatAsync(address, 1);
        return res.Content[0];
    }

    /// <summary>
    /// 读取双精度浮点值。
    /// </summary>
    public async Task<double> ReadDoubleAsync(string address)
    {
        var res = await _device.ReadDoubleAsync(address, 1);
        return res.Content[0];
    }

    /// <summary>
    /// 读取字符串。
    /// </summary>
    public async Task<string> ReadStringAsync(string address, ushort length, Encoding encoding)
    {
        var res = await _device.ReadStringAsync(address, length, encoding);
        return res.Content;
    }

    /// <summary>
    /// 读取布尔值。
    /// </summary>
    public async Task<bool> ReadBoolAsync(string address)
    {
        var res = await _device.ReadBoolAsync(address, 1);
        return res.Content[0];
    }

    /// <summary>
    /// 缓冲区转换为无符号短整型。
    /// </summary>
    public ushort TransUShort(byte[] buffer, int index) => _device.ByteTransform.TransUInt16(buffer, index);
    /// <summary>
    /// 缓冲区转换为无符号整型。
    /// </summary>
    public uint TransUInt(byte[] buffer, int index) => _device.ByteTransform.TransUInt32(buffer, index);
    /// <summary>
    /// 缓冲区转换为无符号长整型。
    /// </summary>
    public ulong TransULong(byte[] buffer, int index) => _device.ByteTransform.TransUInt64(buffer, index);
    /// <summary>
    /// 缓冲区转换为短整型。
    /// </summary>
    public short TransShort(byte[] buffer, int index) => _device.ByteTransform.TransInt16(buffer, index);
    /// <summary>
    /// 缓冲区转换为整型。
    /// </summary>
    public int TransInt(byte[] buffer, int index) => _device.ByteTransform.TransInt32(buffer, index);
    /// <summary>
    /// 缓冲区转换为长整型。
    /// </summary>
    public long TransLong(byte[] buffer, int index) => _device.ByteTransform.TransInt64(buffer, index);
    /// <summary>
    /// 缓冲区转换为单精度浮点值。
    /// </summary>
    public float TransFloat(byte[] buffer, int index) => _device.ByteTransform.TransSingle(buffer, index);
    /// <summary>
    /// 缓冲区转换为双精度浮点值。
    /// </summary>
    public double TransDouble(byte[] buffer, int index) => _device.ByteTransform.TransDouble(buffer, index);
    /// <summary>
    /// 缓冲区转换为字符串。
    /// </summary>
    public string TransString(byte[] buffer, int index, int length, Encoding encoding) => _device.ByteTransform.TransString(buffer, index, length, encoding);
    /// <summary>
    /// 缓冲区转换为布尔值。
    /// </summary>
    public bool TransBool(byte[] buffer, int index) => _device.ByteTransform.TransBool(buffer, index);
}
