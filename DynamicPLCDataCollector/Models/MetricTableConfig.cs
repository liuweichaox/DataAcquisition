namespace DynamicPLCDataCollector.Models;

/// <summary>
/// 采集
/// </summary>
public class MetricTableConfig
{
    /// <summary>
    /// 采集ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 数据库名称
    /// </summary>
    public string DatabaseName { get; set; }
    
    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// 间隔时间（ms）
    /// </summary>
    public int CollectionFrequency { get; set; } 
    
    /// <summary>
    /// 采集配置
    /// </summary>
    public List<MetricColumnConfig> MetricConfigs { get; set; }
}