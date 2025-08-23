using HslCommunication.Core;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 抽象 PLC 客户端的基本读写与批量操作能力。
/// </summary>
public interface IPlcClient
{
    /// <summary>
    /// 读取指定地址的数据字节。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">读取长度</param>
    /// <returns>读取结果</returns>
    OperateResult<byte[]> Read(string address, ushort length);

    /// <summary>
    /// 写入字节到指定地址。
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">待写入数据</param>
    /// <returns>写入结果</returns>
    OperateResult Write(string address, byte[] value);

    /// <summary>
    /// 读取单个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">地址</param>
    /// <returns>读取结果</returns>
    OperateResult<T> Read<T>(string address) where T : struct;

    /// <summary>
    /// 写入单个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">地址</param>
    /// <param name="value">写入的值</param>
    /// <returns>写入结果</returns>
    OperateResult Write<T>(string address, T value) where T : struct;

    /// <summary>
    /// 批量读取多个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="count">读取数量</param>
    /// <returns>读取结果</returns>
    OperateResult<T[]> ReadArray<T>(string address, ushort count) where T : struct;

    /// <summary>
    /// 批量写入多个值。
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="values">写入的值数组</param>
    /// <returns>写入结果</returns>
    OperateResult WriteArray<T>(string address, T[] values) where T : struct;
}

