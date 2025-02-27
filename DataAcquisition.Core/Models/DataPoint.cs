using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DataAcquisition.Core.Models;

/// <summary>
/// 数据点
/// </summary>
public class DataPoint(DateTime timestamp,string tableName)
{
    public DateTime Timestamp => timestamp;
    public string TableName => tableName;
    public ConcurrentDictionary<string, dynamic> Values { get; set; } = new();
}