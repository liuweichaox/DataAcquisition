﻿using System.Threading.Tasks;

namespace DataAcquisition.Services.PlcClients;

/// <summary>
/// PLC 客户端接口定义
/// </summary>
public interface IPlcClient
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
    /// <param name="length"></param>
    /// <returns></returns>
    ushort TransUInt16(byte[] buffer, int length);

    /// <summary>
    /// 转换字节为 uint
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    uint TransUInt32(byte[] buffer, int length);

    /// <summary>
    /// 转换字节为 ulong
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    ulong TransUInt64(byte[] buffer, int length);

    /// <summary>
    /// 转换字节为 short
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    short TransInt16(byte[] buffer, int length);

    /// <summary>
    /// 转换字节为 int
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    int TransInt32(byte[] buffer, int length);

    /// <summary>
    /// 转换字节为 long
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    long TransInt64(byte[] buffer, int length);

    /// <summary>
    /// 转换字节为 float
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    float TransSingle(byte[] buffer, int length);

    /// <summary>
    /// 转换字节为 double
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    double TransDouble(byte[] buffer, int length);

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
    /// <param name="length"></param>
    /// <returns></returns>
    bool TransBool(byte[] buffer, int length);
}