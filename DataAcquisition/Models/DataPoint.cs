using System;
using System.Collections.Generic;

namespace DataAcquisition.Models;

/// <summary>
/// 数据点
/// </summary>
public class DataPoint(string tableName, Dictionary<string, object> values)
{
    public DateTime Timestamp => DateTime.Now;
    public string TableName => tableName;

    public Dictionary<string, object> Values { get; set; } = values;
}