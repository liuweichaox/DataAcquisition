using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using DataAcquisition.Domain.Clients;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 通讯客户端通用接口，封装具体协议实现。
/// </summary>
public interface IPlcClientService
{
    /// <summary>
    /// 关闭连接。
    /// </summary>
    /// <returns>表示异步操作的任务。</returns>
    Task ConnectCloseAsync();

    /// <summary>
    /// Ping 设备 IP。
    /// </summary>
    /// <returns>IP 地址的连通性状态。</returns>
    IPStatus IpAddressPing();

    /// <summary>
    /// 写入无符号短整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteUShortAsync(string address, ushort value);

    /// <summary>
    /// 写入无符号整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteUIntAsync(string address, uint value);

    /// <summary>
    /// 写入无符号长整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteULongAsync(string address, ulong value);

    /// <summary>
    /// 写入短整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteShortAsync(string address, short value);

    /// <summary>
    /// 写入整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteIntAsync(string address, int value);

    /// <summary>
    /// 写入长整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteLongAsync(string address, long value);

    /// <summary>
    /// 写入单精度浮点值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteFloatAsync(string address, float value);

    /// <summary>
    /// 写入双精度浮点值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteDoubleAsync(string address, double value);

    /// <summary>
    /// 写入字符串。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteStringAsync(string address, string value);

    /// <summary>
    /// 写入布尔值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">写入值</param>
    /// <returns>表示写入结果的任务。</returns>
    Task<PlcWriteResult> WriteBoolAsync(string address, bool value);

    /// <summary>
    /// 批量读取原始数据。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">长度</param>
    /// <returns>表示读取结果的任务。</returns>
    Task<PlcReadResult> ReadAsync(string address, ushort length);

    /// <summary>
    /// 读取无符号短整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <returns>表示读取结果的任务。</returns>
    Task<ushort> ReadUShortAsync(string address);

    /// <summary>
    /// 读取无符号整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <returns>表示读取结果的任务。</returns>
    Task<uint> ReadUIntAsync(string address);

    /// <summary>
    /// 读取无符号长整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <returns>表示读取结果的任务。</returns>
    Task<ulong> ReadULongAsync(string address);

    /// <summary>
    /// 读取短整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <returns>表示读取结果的任务。</returns>
    Task<short> ReadShortAsync(string address);

    /// <summary>
    /// 读取整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <returns>表示读取结果的任务。</returns>
    Task<int> ReadIntAsync(string address);

    /// <summary>
    /// 读取长整型值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <returns>表示读取结果的任务。</returns>
    Task<long> ReadLongAsync(string address);

    /// <summary>
    /// 读取单精度浮点值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <returns>表示读取结果的任务。</returns>
    Task<float> ReadFloatAsync(string address);

    /// <summary>
    /// 读取双精度浮点值。
    /// </summary>
    /// <param name="address">寄存器地址</param>
    /// <returns>表示读取结果的任务。</returns>
    Task<double> ReadDoubleAsync(string address);

    /// <summary>
    /// 将字节数组转换为无符号短整型。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <returns>转换得到的值。</returns>
    ushort TransUShort(byte[] buffer, int index);

    /// <summary>
    /// 将字节数组转换为无符号整型。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <returns>转换得到的值。</returns>
    uint TransUInt(byte[] buffer, int index);

    /// <summary>
    /// 将字节数组转换为无符号长整型。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <returns>转换得到的值。</returns>
    ulong TransULong(byte[] buffer, int index);

    /// <summary>
    /// 将字节数组转换为短整型。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <returns>转换得到的值。</returns>
    short TransShort(byte[] buffer, int index);

    /// <summary>
    /// 将字节数组转换为整型。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <returns>转换得到的值。</returns>
    int TransInt(byte[] buffer, int index);

    /// <summary>
    /// 将字节数组转换为长整型。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <returns>转换得到的值。</returns>
    long TransLong(byte[] buffer, int index);

    /// <summary>
    /// 将字节数组转换为单精度浮点值。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <returns>转换得到的值。</returns>
    float TransFloat(byte[] buffer, int index);

    /// <summary>
    /// 将字节数组转换为双精度浮点值。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <returns>转换得到的值。</returns>
    double TransDouble(byte[] buffer, int index);

    /// <summary>
    /// 将字节数组转换为字符串。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <param name="length">字节长度</param>
    /// <param name="encoding">字符串编码</param>
    /// <returns>转换得到的字符串。</returns>
    string TransString(byte[] buffer, int index, int length, Encoding encoding);

    /// <summary>
    /// 将字节数组转换为布尔值。
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="index">起始索引</param>
    /// <returns>转换得到的值。</returns>
    bool TransBool(byte[] buffer, int index);
}
