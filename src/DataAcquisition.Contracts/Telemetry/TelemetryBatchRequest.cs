namespace DataAcquisition.Contracts.Telemetry;

/// <summary>
/// 采集数据批量上报：边缘->中心。
/// </summary>
public sealed record TelemetryBatchRequest
{
    public required string EdgeId { get; init; }

    /// <summary>
    /// 可选：幂等键（同一批次重试不应重复入库）。
    /// </summary>
    public string? BatchId { get; init; }

    public required List<TelemetryPoint> Points { get; init; }
}

public sealed record TelemetryPoint
{
    public required string DeviceId { get; init; }
    public required string Channel { get; init; }
    public required double Value { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

