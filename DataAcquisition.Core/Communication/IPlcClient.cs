using HslCommunication.Core;
using System.Threading.Tasks;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 抽象 PLC 客户端的基本连接、读写与批量操作能力。
/// </summary>
public interface IPlcClient
{
    /// <summary>
    /// 建立与 PLC 的连接。
    /// </summary>
    /// <returns>连接结果</returns>
    OperateResult Connect();

    /// <summary>
    /// 断开连接。
    /// </summary>
    void Close();

    /// <summary>
    /// 读取指定地址的数据字节。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">读取长度</param>
    /// <returns>读取结果</returns>
    OperateResult<byte[]> Read(string address, ushort length);

    /// <summary>
    /// 异步读取指定地址的数据字节。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">读取长度</param>
    /// <returns>读取结果</returns>
    Task<OperateResult<byte[]>> ReadAsync(string address, ushort length);

    /// <summary>
    /// 写入字节到指定地址。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">待写入数据</param>
    /// <returns>写入结果</returns>
    OperateResult Write(string address, byte[] value);

    /// <summary>
    /// 异步写入字节到指定地址。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">待写入数据</param>
    /// <returns>写入结果</returns>
    Task<OperateResult> WriteAsync(string address, byte[] value);

    /// <summary>
    /// 读取单个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">地址</param>
    /// <returns>读取结果</returns>
    OperateResult<T> Read<T>(string address) where T : struct;

    /// <summary>
    /// 异步读取单个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">地址</param>
    /// <returns>读取结果</returns>
    Task<OperateResult<T>> ReadAsync<T>(string address) where T : struct;

    /// <summary>
    /// 写入单个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">地址</param>
    /// <param name="value">写入的值</param>
    /// <returns>写入结果</returns>
    OperateResult Write<T>(string address, T value) where T : struct;

    /// <summary>
    /// 异步写入单个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">地址</param>
    /// <param name="value">写入的值</param>
    /// <returns>写入结果</returns>
    Task<OperateResult> WriteAsync<T>(string address, T value) where T : struct;

    /// <summary>
    /// 批量读取多个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="count">读取数量</param>
    /// <returns>读取结果</returns>
    OperateResult<T[]> ReadArray<T>(string address, ushort count) where T : struct;

    /// <summary>
    /// 异步批量读取多个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="count">读取数量</param>
    /// <returns>读取结果</returns>
    Task<OperateResult<T[]>> ReadArrayAsync<T>(string address, ushort count) where T : struct;

    /// <summary>
    /// 批量写入多个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="values">写入的值数组</param>
    /// <returns>写入结果</returns>
    OperateResult WriteArray<T>(string address, T[] values) where T : struct;

    /// <summary>
    /// 异步批量写入多个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="values">写入的值数组</param>
    /// <returns>写入结果</returns>
    Task<OperateResult> WriteArrayAsync<T>(string address, T[] values) where T : struct;
}

