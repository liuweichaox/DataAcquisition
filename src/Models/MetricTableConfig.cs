namespace DynamicPLCDataCollector.Models;

/// <summary>
/// 采集表配置
/// </summary>
public class MetricTableConfig
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// 数据库名称
    /// </summary>
    public string DatabaseName { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 间隔时间（ms）
    /// </summary>
    public int CollectionFrequency { get; set; }

    /// <summary>
    /// 批量保存的数据
    /// </summary>
    public int BatchSize { get; set; }
    
    /// <summary>
    /// 是否需要添加时间戳列
    /// </summary>
    public bool IsAddDateTimeNow { get; set; }
    
    /// <summary>
    /// 是否使用 Utc 时间戳
    /// </summary>
    public bool IsUtc {  get; set; }
    
    /// <summary>
    /// 时间戳列名
    /// </summary>
    public string DateTimeNowColumnName { get; set; }

    /// <summary>
    /// 采集配置
    /// </summary>
    public List<MetricColumnConfig> MetricColumnConfigs { get; set; }
}