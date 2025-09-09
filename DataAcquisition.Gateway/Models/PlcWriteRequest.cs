using System.Collections.Generic;
using System.Text.Json;

namespace DataAcquisition.Gateway.Models;

/// <summary>
/// PLC 批量写入请求
/// </summary>
public class PlcWriteRequest
{
    /// <summary>
    /// PLC 编号
    /// </summary>
    public string PlcCode { get; set; } = string.Empty;

    /// <summary>
    /// 写入项集合
    /// </summary>
    public List<PlcWriteItem> Items { get; set; } = new();
}

/// <summary>
/// PLC 写入项
/// </summary>
public class PlcWriteItem
{
    /// <summary>
    /// 寄存器地址
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// 写入值
    /// </summary>
    public object Value { get; set; }
}

