namespace DynamicPLCDataCollector.Models;

/// <summary>
/// 采集列配置
/// </summary>
public class MetricColumnConfig
{
    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// 数据地址
    /// </summary>
    public string DataAddress { get; set; }

    /// <summary>
    /// 数据长度
    /// </summary>
    public ushort DataLength { get; set; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; }
}