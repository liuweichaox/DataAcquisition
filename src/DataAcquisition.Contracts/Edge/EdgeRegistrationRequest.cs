namespace DataAcquisition.Contracts.Edge;

/// <summary>
/// 边缘节点注册（或更新）请求：用于让中心“认识”这个车间采集节点。
/// </summary>
public sealed record EdgeRegistrationRequest
{
    public required string WorkshopId { get; init; }
    public required string EdgeId { get; init; }

    /// <summary>
    /// 可选：机器名/容器名/实例标识（便于排障）。
    /// </summary>
    public string? Hostname { get; init; }

    /// <summary>
    /// 可选：边缘程序版本。
    /// </summary>
    public string? Version { get; init; }
}

