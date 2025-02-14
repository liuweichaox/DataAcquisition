using System.Collections.Generic;

namespace DataAcquisition.Models;

/// <summary>
/// 采集表配置
/// </summary>
public class DataAcquisitionConfig
{
    /// <summary>
    /// 设备编码
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// IP地址
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; }
    
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
    /// 采集位置配置
    /// </summary>
    public List<PositionConfig> PositionConfigs { get; set; }
}

/// <summary>
/// 采集位置配置
/// </summary>
public class PositionConfig
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
    
    /// <summary>
    /// 数据表达式计算
    /// </summary>
    public string EvalExpression { get; set; }
}