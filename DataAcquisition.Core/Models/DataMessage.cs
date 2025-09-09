using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DataAcquisition.Core.Models;

/// <summary>
/// 数据点
/// </summary>
public class DataMessage(DateTime timestamp, string tableName, int batchSize, List<DataPoint>? dataPoints = null, DataOperation operation = DataOperation.Insert)
{
    public DateTime Timestamp => timestamp;
    public string TableName => tableName;
    public int BatchSize => batchSize;
    public List<DataPoint>? DataPoints => dataPoints;
    public DataOperation Operation => operation;
    public ConcurrentDictionary<string, dynamic?> DataValues { get; } = new();
    public ConcurrentDictionary<string, dynamic> KeyValues { get; } = new();
}
