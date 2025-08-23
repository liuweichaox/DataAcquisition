using HslCommunication.Core;
using HslCommunication.Profinet.Inovance;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 汇川 TCP 驱动实现。
/// </summary>
public class InovanceTcpNetClient : InovanceTcpNet, IPlcClient
{
    public InovanceTcpNetClient(string ipAddress, int port) : base(ipAddress, port)
    {
    }

    /// <inheritdoc />
    public bool SupportsBatchRead => true;

    /// <inheritdoc />
    public OperateResult<byte[]> ReadBatch(string register, ushort length)
    {
        return Read(register, length);
    }
}

