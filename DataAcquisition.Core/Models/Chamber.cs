using System;

namespace DataAcquisition.Core.Models;

/// <summary>
/// 腔室
/// </summary>
public class Chamber
{

    public Chamber(string chamberCode)
    {
        ChamberCode = chamberCode;
    }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName => $"tb_{ChamberCode}batch";
    
    /// <summary>
    /// 腔室编号
    /// </summary>
    public string ChamberCode { get; set; }
    
    /// <summary>
    /// 批次流水号
    /// </summary>
    public string BatchSequence { get; set; }
    
    /// <summary>
    /// 配方编号
    /// </summary>
    public string RecipeCode { get; set; }

    /// <summary>
    /// 运行状态
    /// </summary>
    public MachineStatus Status { get; set; }
            
    /// <summary>
    /// 批次号
    /// </summary>
    public ushort? BatchNumber { get; set; }
            
    /// <summary>
    /// 开始生产时间
    /// </summary>
    public DateTime? StarTime { get; set; }

    /// <summary>
    /// 结束生产时间
    /// </summary>
    public DateTime? EndTime { get; set; }
}

public enum MachineStatus
{
    Idle = 0, 
    Starting = 1, 
    Producing = 2, 
    Stopping = 3
}