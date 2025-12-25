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
    /// 可选：Edge.Agent 对中心可达的基础地址（例如 http://10.0.0.12:8001）。
    /// 用于中心侧代理查询 edge 的 metrics / logs。
    /// 为空时会尝试从 Urls/ASPNETCORE_URLS 推导（如果是 0.0.0.0/+/* 之类通配监听地址则无法推导）。
    /// </summary>
    public string? AgentBaseUrl { get; init; }

    /// <summary>
    /// 可选：固定 EdgeId。为空时会从 IdentityFilePath 读取/生成并持久化。
    /// </summary>
    public string? EdgeId { get; init; }

    /// <summary>
    /// 心跳间隔（秒）。
    /// </summary>
    public int HeartbeatIntervalSeconds { get; init; } = 10;
}

