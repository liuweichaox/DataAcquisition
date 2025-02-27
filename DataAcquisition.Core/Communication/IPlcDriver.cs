using System.Threading.Tasks;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// PLC 客户端接口定义
/// </summary>
public interface IPlcDriver
{
    /// <summary>
    /// 连接到 PLC 设备
    /// </summary>
    /// <returns></returns>
    Task<OperationResult<bool>> ConnectServerAsync();

    /// <summary>
    /// 断开与 PLC 设备的连接
    /// </summary>
    /// <returns></returns>
    Task<OperationResult<bool>> ConnectCloseAsync();

    /// <summary>
    /// Ping 检测 PLC 是否可达
    /// </summary>
    /// <returns></returns>
    Task<OperationResult<bool>> IpAddressPingAsync();

    /// <summary>
    /// 批量读取
    /// </summary>
    /// <param name="address"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    Task<OperationResult<byte[]>> ReadAsync(string address, ushort length);

    /// <summary>
    /// 转换字节为 ushort
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    ushort TransUInt16(byte[] buffer, int index);

    /// <summary>
    /// 转换字节为 uint
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    uint TransUInt32(byte[] buffer, int index);

    /// <summary>
    /// 转换字节为 ulong
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    ulong TransUInt64(byte[] buffer, int index);

    /// <summary>
    /// 转换字节为 short
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    short TransInt16(byte[] buffer, int index);

    /// <summary>
    /// 转换字节为 int
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    int TransInt32(byte[] buffer, int index);

    /// <summary>
    /// 转换字节为 long
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    long TransInt64(byte[] buffer, int index);

    /// <summary>
    /// 转换字节为 float
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    float TransSingle(byte[] buffer, int index);

    /// <summary>
    /// 转换字节为 double
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    double TransDouble(byte[] buffer, int index);

    /// <summary>
    /// 转换字节为 string
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <param name="length"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    string TransString(byte[] buffer, int index, int length, string encoding);

    /// <summary>
    /// 转换字节为 bool
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    bool TransBool(byte[] buffer, int index);

    /// <summary>
    /// 写入 UInt16
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    OperationResult WriteUInt16(string address, ushort value);

    /// <summary>
    /// 写入 UInt32
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    OperationResult WriteUInt32(string address, uint value);

    /// <summary>
    /// 写入 UInt64
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    OperationResult WriteUInt64(string address, ulong value);

    /// <summary>
    /// 写入 Int16
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    OperationResult WriteInt16(string address, short value);

    /// <summary>
    /// 写入 Int32
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    OperationResult WriteInt32(string address, int value);

    /// <summary>
    /// 写入 Int64
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    OperationResult WriteInt64(string address, long value);

    /// <summary>
    /// 写入 float
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    OperationResult WriteSingle(string address, float value);

    /// <summary>
    /// 写入 double
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    OperationResult WriteDouble(string address, double value);

    /// <summary>
    /// 写入 string
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    OperationResult WriteString(string address, string value, Encoding encoding);

    /// <summary>
    /// 写入 bool
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    OperationResult WriteBool(string address, bool value);
}