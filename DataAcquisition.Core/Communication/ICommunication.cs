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
    /// <param name="value">写入值，支持多种数据类型</param>
    Task<CommunicationWriteResult> WriteAsync(string address, object value);

    /// <summary>
    /// 批量读取原始数据。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">长度</param>
    CommunicationReadResult Read(string address, ushort length);

    ushort ReadUInt16(string address);
    uint ReadUInt32(string address);
    ulong ReadUInt64(string address);
    short ReadInt16(string address);
    int ReadInt32(string address);
    long ReadInt64(string address);
    float ReadFloat(string address);
    double ReadDouble(string address);

    ushort TransUInt16(byte[] buffer, int index);
    uint TransUInt32(byte[] buffer, int index);
    ulong TransUInt64(byte[] buffer, int index);
    short TransInt16(byte[] buffer, int index);
    int TransInt32(byte[] buffer, int index);
    long TransInt64(byte[] buffer, int index);
    float TransSingle(byte[] buffer, int index);
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
