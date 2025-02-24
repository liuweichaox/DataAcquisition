using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DataAcquisition.Models;

/// <summary>
/// 数据点
/// </summary>
public class DataPoint(string tableName)
{
    public DateTime Timestamp => DateTime.Now;
    public string TableName => tableName;
    public ConcurrentDictionary<string, dynamic> Values => new();
}