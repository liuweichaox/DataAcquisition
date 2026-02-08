using System;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using HslCommunication;
using HslCommunication.Core.Device;

namespace DataAcquisition.Infrastructure.Clients;

/// <summary>
///     基于 HslCommunication 的 PLC 通讯客户端基类。
///     所有品牌的读写逻辑完全一致，仅底层 DeviceTcpNet 实现不同。
///     如需替换 PLC 通讯库，实现 IPlcClientService 即可。
/// </summary>
public abstract class HslPlcClientServiceBase(DeviceTcpNet device) : IPlcClientService
{
    protected DeviceTcpNet Device { get; } = device;

    // ─── 连接管理 ──────────────────────────────────

    public Task ConnectCloseAsync() => Device.ConnectCloseAsync();
    public IPStatus IpAddressPing() => Device.IpAddressPing();

    // ─── 写入（统一委托到 Device.WriteAsync 重载）───

    private async Task<PlcWriteResult> WriteAsync(Func<Task<OperateResult>> write)
    {
        var res = await write();
        return new PlcWriteResult { IsSuccess = res.IsSuccess, Message = res.Message };
    }

    public Task<PlcWriteResult> WriteUShortAsync(string address, ushort value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    public Task<PlcWriteResult> WriteUIntAsync(string address, uint value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    public Task<PlcWriteResult> WriteULongAsync(string address, ulong value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    public Task<PlcWriteResult> WriteShortAsync(string address, short value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    public Task<PlcWriteResult> WriteIntAsync(string address, int value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    public Task<PlcWriteResult> WriteLongAsync(string address, long value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    public Task<PlcWriteResult> WriteFloatAsync(string address, float value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    public Task<PlcWriteResult> WriteDoubleAsync(string address, double value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    public Task<PlcWriteResult> WriteStringAsync(string address, string value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    public Task<PlcWriteResult> WriteBoolAsync(string address, bool value) =>
        WriteAsync(() => Device.WriteAsync(address, value));

    // ─── 读取 ──────────────────────────────────────

    public async Task<PlcReadResult> ReadAsync(string address, ushort length)
    {
        var res = await Device.ReadAsync(address, length);
        return new PlcReadResult
        {
            IsSuccess = res.IsSuccess,
            Content = res.Content ?? Array.Empty<byte>(),
            Message = res.Message
        };
    }

    private async Task<T> ReadAsync<T>(Task<OperateResult<T>> readTask)
    {
        var res = await readTask;
        if (!res.IsSuccess)
            throw new InvalidOperationException($"PLC read failed: {res.Message}");
        return res.Content;
    }

    public Task<ushort> ReadUShortAsync(string address) => ReadAsync(Device.ReadUInt16Async(address));
    public Task<uint> ReadUIntAsync(string address) => ReadAsync(Device.ReadUInt32Async(address));
    public Task<ulong> ReadULongAsync(string address) => ReadAsync(Device.ReadUInt64Async(address));
    public Task<short> ReadShortAsync(string address) => ReadAsync(Device.ReadInt16Async(address));
    public Task<int> ReadIntAsync(string address) => ReadAsync(Device.ReadInt32Async(address));
    public Task<long> ReadLongAsync(string address) => ReadAsync(Device.ReadInt64Async(address));
    public Task<float> ReadFloatAsync(string address) => ReadAsync(Device.ReadFloatAsync(address));
    public Task<double> ReadDoubleAsync(string address) => ReadAsync(Device.ReadDoubleAsync(address));

    public async Task<string> ReadStringAsync(string address, ushort length, Encoding encoding) =>
        await ReadAsync(Device.ReadStringAsync(address, length, encoding));

    public async Task<bool> ReadBoolAsync(string address) => await ReadAsync(Device.ReadBoolAsync(address));

    // ─── 字节缓冲区转换 ────────────────────────────

    public ushort TransUShort(byte[] buffer, int index) => Device.ByteTransform.TransUInt16(buffer, index);
    public uint TransUInt(byte[] buffer, int index) => Device.ByteTransform.TransUInt32(buffer, index);
    public ulong TransULong(byte[] buffer, int index) => Device.ByteTransform.TransUInt64(buffer, index);
    public short TransShort(byte[] buffer, int index) => Device.ByteTransform.TransInt16(buffer, index);
    public int TransInt(byte[] buffer, int index) => Device.ByteTransform.TransInt32(buffer, index);
    public long TransLong(byte[] buffer, int index) => Device.ByteTransform.TransInt64(buffer, index);
    public float TransFloat(byte[] buffer, int index) => Device.ByteTransform.TransSingle(buffer, index);
    public double TransDouble(byte[] buffer, int index) => Device.ByteTransform.TransDouble(buffer, index);

    public string TransString(byte[] buffer, int index, int length, Encoding encoding) =>
        Device.ByteTransform.TransString(buffer, index, length, encoding);

    public bool TransBool(byte[] buffer, int index) => Device.ByteTransform.TransBool(buffer, index);

    /// <summary>创建带默认超时的 DeviceTcpNet 实例。</summary>
    protected static T CreateDevice<T>(DeviceConfig config) where T : DeviceTcpNet
    {
        var device = (T)Activator.CreateInstance(typeof(T), config.Host, config.Port)!;
        device.ReceiveTimeOut = 5000;
        device.ConnectTimeOut = 5000;
        return device;
    }
}
