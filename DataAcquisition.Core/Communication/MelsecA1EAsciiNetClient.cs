using HslCommunication.Core;
using HslCommunication.Profinet.Melsec;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 三菱 A1E Ascii 驱动实现。
/// </summary>
public class MelsecA1EAsciiNetClient : MelsecA1EAsciiNet, IPlcClient
{
    public MelsecA1EAsciiNetClient(string ipAddress, int port) : base(ipAddress, port)
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

