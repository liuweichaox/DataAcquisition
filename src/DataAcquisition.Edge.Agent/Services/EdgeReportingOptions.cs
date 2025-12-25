namespace DataAcquisition.Edge.Agent.Services;

/// <summary>
/// Edge 节点向中心上报（注册/心跳）的配置项。
/// </summary>
public sealed class EdgeReportingOptions
{
    /// <summary>
    /// 是否启用向中心注册/心跳上报。
    /// </summary>
    public bool EnableCentralReporting { get; init; } = true;

    /// <summary>
    /// 中心 API 基地址（例如 http://localhost:8000）。
    /// </summary>
    public string CentralApiBaseUrl { get; init; } = "http://localhost:8000";

    /// <summary>
    /// 可选：固定 EdgeId。为空时会从 IdentityFilePath 读取/生成并持久化。
    /// </summary>
    public string? EdgeId { get; init; }

    /// <summary>
    /// EdgeId 本地持久化文件路径（相对路径会基于应用目录）。
    /// </summary>
    public string IdentityFilePath { get; init; } = "Data/edge-id.txt";

    /// <summary>
    /// 心跳间隔（秒）。
    /// </summary>
    public int HeartbeatIntervalSeconds { get; init; } = 10;
}

