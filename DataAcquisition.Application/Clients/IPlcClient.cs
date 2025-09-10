using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using DataAcquisition.Domain.Clients;

namespace DataAcquisition.Application.Clients;

/// <summary>
/// 通讯客户端通用接口，封装具体协议实现。
/// </summary>
public interface IPlcClient
{
    /// <summary>
    /// 关闭连接。
    /// </summary>
    Task ConnectCloseAsync();

    /// <summary>
    /// Ping 设备 IP。
    /// </summary>
    IPStatus IpAddressPing();

    /// <summary>
    /// 写寄存器。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    Task<PlcWriteResult> WriteUShortAsync(string address, ushort value);
    Task<PlcWriteResult> WriteUIntAsync(string address, uint value);
    Task<PlcWriteResult> WriteULongAsync(string address, ulong value);
    Task<PlcWriteResult> WriteShortAsync(string address, short value);
    Task<PlcWriteResult> WriteIntAsync(string address, int value);
    Task<PlcWriteResult> WriteLongAsync(string address, long value);
    Task<PlcWriteResult> WriteFloatAsync(string address, float value);
    Task<PlcWriteResult> WriteDoubleAsync(string address, double value);
    Task<PlcWriteResult> WriteStringAsync(string address, string value);
    Task<PlcWriteResult> WriteBoolAsync(string address, bool value);

    /// <summary>
    /// 批量读取原始数据。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">长度</param>
    Task<PlcReadResult> ReadAsync(string address, ushort length);

    Task<ushort> ReadUShortAsync(string address);
    Task<uint> ReadUIntAsync(string address);
    Task<ulong> ReadULongAsync(string address);
    Task<short> ReadShortAsync(string address);
    Task<int> ReadIntAsync(string address);
    Task<long> ReadLongAsync(string address);
    Task<float> ReadFloatAsync(string address);
    Task<double> ReadDoubleAsync(string address);

    ushort TransUShort(byte[] buffer, int index);
    uint TransUInt(byte[] buffer, int index);
    ulong TransULong(byte[] buffer, int index);
    short TransShort(byte[] buffer, int index);
    int TransInt(byte[] buffer, int index);
    long TransLong(byte[] buffer, int index);
    float TransFloat(byte[] buffer, int index);
    double TransDouble(byte[] buffer, int index);
    string TransString(byte[] buffer, int index, int length, Encoding encoding);
    bool TransBool(byte[] buffer, int index);
}
