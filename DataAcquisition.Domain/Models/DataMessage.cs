using System;
using System.Collections.Concurrent;

namespace DataAcquisition.Domain.Models;

/// <summary>
/// 数据点消息
/// </summary>
public class DataMessage(DateTime timestamp, string tableName, int batchSize, DataOperation operation = DataOperation.Insert)
{
    public DateTime Timestamp => timestamp;
    public string TableName => tableName;
    public int BatchSize => batchSize;
    public DataOperation Operation => operation;
    public ConcurrentDictionary<string, dynamic?> DataValues { get; } = new();
    public ConcurrentDictionary<string, dynamic> KeyValues { get; } = new();
}

