using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 通讯客户端通用接口，封装具体协议实现。
/// </summary>
public interface ICommunication
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
    Task<CommunicationWriteResult> WriteUShortAsync(string address, ushort value);
    Task<CommunicationWriteResult> WriteUIntAsync(string address, uint value);
    Task<CommunicationWriteResult> WriteULongAsync(string address, ulong value);
    Task<CommunicationWriteResult> WriteShortAsync(string address, short value);
    Task<CommunicationWriteResult> WriteIntAsync(string address, int value);
    Task<CommunicationWriteResult> WriteLongAsync(string address, long value);
    Task<CommunicationWriteResult> WriteFloatAsync(string address, float value);
    Task<CommunicationWriteResult> WriteDoubleAsync(string address, double value);
    Task<CommunicationWriteResult> WriteStringAsync(string address, string value);
    Task<CommunicationWriteResult> WriteBoolAsync(string address, bool value);

    /// <summary>
    /// 批量读取原始数据。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">长度</param>
    Task<CommunicationReadResult> ReadAsync(string address, ushort length);

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

/// <summary>
/// 读取结果
/// </summary>
public class CommunicationReadResult
{
    public bool IsSuccess { get; set; }
    public byte[] Content { get; set; } = System.Array.Empty<byte>();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 写入结果
/// </summary>
public class CommunicationWriteResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}
