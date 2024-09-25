namespace DynamicPLCDataCollector.Models;

/// <summary>
/// 采集配置
/// </summary>
public class MetricColumnConfig
{
    /// <summary>
    /// 配置ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 采集ID
    /// </summary>
    public int MetricId { get; set; }
    
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