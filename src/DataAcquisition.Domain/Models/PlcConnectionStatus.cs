using System;

namespace DataAcquisition.Domain.Models;

/// <summary>
///     PLC 连接状态详细信息
/// </summary>
public sealed class PlcConnectionStatus
{
    /// <summary>
    ///     PLC 编码
    /// </summary>
    public required string PlcCode { get; init; }

    /// <summary>
    ///     是否已连接
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    ///     最后连接时间（本地时间）
    /// </summary>
    public DateTimeOffset? LastConnectedTime { get; init; }

    /// <summary>
    ///     连接持续时间（秒），如果当前已断开则为 null
    /// </summary>
    public double? ConnectionDurationSeconds { get; init; }

    /// <summary>
    ///     最后一次错误信息（如果存在）
    /// </summary>
    public string? LastError { get; init; }
}

