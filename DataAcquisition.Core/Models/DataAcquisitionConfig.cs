﻿using System.Collections.Generic;

namespace DataAcquisition.Core.Models;

/// <summary>
/// 采集表配置
/// </summary>
public class DataAcquisitionConfig
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 采集间隔时间（ms）
    /// </summary>
    public int CollectIntervalMs { get; set; }

    /// <summary>
    /// 心跳检测间隔时间（ms）
    /// </summary>
    public int HeartbeatIntervalMs { get; set; }
    
    /// <summary>
    /// 驱动类型
    /// </summary>
    public string DriverType { get; set; }
    
    /// <summary>
    /// 数据库类型
    /// </summary>
    public string StorageType { get; set; }
    
    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// PLC 配置
    /// </summary>
    public PlcConfig Plc { get; set; }
}

/// <summary>
/// PLC 配置
/// </summary>
public class PlcConfig
{
    /// <summary>
    /// PLC 编码
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
    /// 批量读取字节数组信息地址
    /// </summary>
    public string BatchReadAddress { get; set; }

    /// <summary>
    /// 批量读取字节数组信息地址
    /// </summary>
    public ushort BatchReadLength { get; set; }

    /// <summary>
    /// 寄存器分组
    /// </summary>
    public RegisterGroup[] RegisterGroups { get; set; }
}

/// <summary>
/// 寄存器分组
/// </summary>
public class RegisterGroup
{
    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 批量大小
    /// </summary>
    public int BatchSize { get; set; }

    /// <summary>
    /// 采集位置配置
    /// </summary>
    public List<Register> Registers { get; set; }
}

/// <summary>
/// 采集位置配置
/// </summary>
public class Register
{
    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; }

    /// <summary>
    /// 索引位置
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 字符串 byte 数组长度
    /// </summary>
    public int? StringByteLength { get; set; }

    /// <summary>
    /// 仅用于字符串,字符串编码格式
    /// </summary>
    public string Encoding { get; set; }

    /// <summary>
    /// 数据表达式计算
    /// </summary>
    public string EvalExpression { get; set; }
}