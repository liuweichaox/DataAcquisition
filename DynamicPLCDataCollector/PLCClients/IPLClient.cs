using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.PLCClients;

public interface IPLClient
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
    /// 检查设备是否在线
    /// </summary>
    /// <returns></returns>
    bool IsConnected();

    /// <summary>
    /// 读取整数
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<OperationResult<int>> ReadInt32Async(string address);

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
