using System;
using System.Collections.Generic;
using System.Linq;

namespace DataAcquisition.Models;

/// <summary>
/// 数据点
/// </summary>
public class DataPoint(Dictionary<string, object> values)
{
    public DateTime Timestamp => DateTime.Now;
    public Dictionary<string,object> Values { get; set; } = values;
    public override int GetHashCode()
    {
        // 使用字典键值对计算哈希值
        unchecked
        {
            int hash = 17;
            foreach (var kvp in Values.OrderBy(k => k.Key))
            {
                hash = hash * 31 + kvp.Key.GetHashCode();
                hash = hash * 31 + (kvp.Value?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is DataPoint other)
        {
            return Values.OrderBy(k => k.Key).SequenceEqual(other.Values.OrderBy(k => k.Key));
        }
        return false;
    }
}