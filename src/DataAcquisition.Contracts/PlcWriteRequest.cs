namespace DataAcquisition.Contracts;

/// <summary>
///     PLC 批量写入请求
/// </summary>
public class PLCWriteRequest
{
    /// <summary>
    ///     PLC 编号
    /// </summary>
    public string PLCCode { get; set; } = string.Empty;

    /// <summary>
    ///     写入项集合
    /// </summary>
    public List<PLCWriteItem> Items { get; set; } = new();
}

/// <summary>
///     PLC 写入项
/// </summary>
public class PLCWriteItem
{
    /// <summary>
    ///     寄存器地址
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    ///     数据类型
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    ///     写入值
    /// </summary>
    public object? Value { get; set; }
}