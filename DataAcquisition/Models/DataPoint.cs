using System;
using System.Collections.Generic;

namespace DataAcquisition.Models;

/// <summary>
/// 数据点
/// </summary>
public class DataPoint
{
    public DateTime Timestamp => DateTime.Now;
    public Dictionary<string,object> Values { get; set; }

    public DataPoint(Dictionary<string, object> values)
    {
        Values = values;
    }
}