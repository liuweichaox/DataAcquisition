namespace DataAcquisition.Contracts.Edge;

/// <summary>
/// 边缘节点心跳：中心用它更新在线状态与运行指标。
/// </summary>
public sealed record EdgeHeartbeatRequest
{
    public required string EdgeId { get; init; }

    /// <summary>
    /// 可选：边缘本地缓冲积压量（条数/批次/字节）。
    /// </summary>
    public long? BufferBacklog { get; init; }

    /// <summary>
    /// 可选：最后一次错误摘要。
    /// </summary>
    public string? LastError { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

