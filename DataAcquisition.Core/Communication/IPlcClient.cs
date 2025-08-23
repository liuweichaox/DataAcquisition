using HslCommunication.Core;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 定义 PLC 客户端的通用能力。
/// </summary>
public interface IPlcClient
{
    /// <summary>
    /// 指示当前驱动是否支持批量读取。
    /// </summary>
    bool SupportsBatchRead { get; }

    /// <summary>
    /// 批量读取寄存器。
    /// </summary>
    /// <param name="register">起始寄存器地址。</param>
    /// <param name="length">读取字节长度。</param>
    /// <returns>读取结果。</returns>
    OperateResult<byte[]> ReadBatch(string register, ushort length);
}

