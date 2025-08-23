using HslCommunication.Core;
using HslCommunication.Profinet.Melsec;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 三菱 A1E Net 驱动实现。
/// </summary>
public class MelsecA1ENetClient : MelsecA1ENet, IPlcClient
{
    public MelsecA1ENetClient(string ipAddress, int port) : base(ipAddress, port)
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

