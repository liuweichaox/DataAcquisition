namespace DataAcquisition.Domain.Clients;

/// <summary>
///     PLC 写入结果。
/// </summary>
public class PLCWriteResult
{
    /// <summary>
    ///     写入是否成功。
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    ///     结果描述信息。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}