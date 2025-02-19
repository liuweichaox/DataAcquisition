using System;
using System.Threading.Tasks;

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
    /// 读取 16 位无符号整数
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<UInt16>> ReadUInt16Async(string address);

    /// <summary>
    /// 读取 32 位无符号整数
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<UInt32>> ReadUInt32Async(string address);
    
    /// <summary>
    /// 读取 64 位无符号整数
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<UInt64>> ReadUInt64Async(string address);
    
    /// <summary>
    /// 读取 16 位带符号整数
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<Int16>> ReadInt16Async(string address);

    /// <summary>
    /// 读取 32 位带符号整数
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<Int32>> ReadInt32Async(string address);
    
    /// <summary>
    /// 读取 64 位带符号整数
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<Int64>> ReadInt64Async(string address);

    /// <summary>
    /// 读取浮点数
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<float>> ReadFloatAsync(string address);

    /// <summary>
    /// 读取双精度浮点数
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<double>> ReadDoubleAsync(string address);

    /// <summary>
    /// 读取字符串
    /// </summary>
    /// <param name="address"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    Task<OperationResult<string>> ReadStringAsync(string address, ushort length);

    /// <summary>
    /// 读取布尔值
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<bool>> ReadBoolAsync(string address);
}