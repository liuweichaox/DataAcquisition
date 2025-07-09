using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DataAcquisition.Core.Models;

/// <summary>
/// 数据点
/// </summary>
public class DataMessage(DateTime timestamp, string tableName, List<DataPoint>? dataPoints = null)
{
    public DateTime Timestamp => timestamp;
    public string TableName => tableName;
    public List<DataPoint>? DataPoints => dataPoints;
    public ConcurrentDictionary<string, dynamic> Values { get; } = new();
}
