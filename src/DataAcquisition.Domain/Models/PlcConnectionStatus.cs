using System;

namespace DataAcquisition.Domain.Models;

/// <summary>
///     Plc 连接状态详细信息
/// </summary>
public sealed class PlcConnectionStatus
{
    /// <summary>
    ///     Plc 编码
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

    /// <summary>
    ///     当前离线时长（秒），如果当前已连接则为 null
    /// </summary>
    public double? DisconnectedDurationSeconds { get; init; }

    /// <summary>
    ///     总重连次数
    /// </summary>
    public int TotalReconnectCount { get; init; }

    /// <summary>
    ///     最后断开时间（本地时间），如果从未断开则为 null
    /// </summary>
    public DateTimeOffset? LastDisconnectedTime { get; init; }
}

