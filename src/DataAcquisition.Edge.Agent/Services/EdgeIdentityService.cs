using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataAcquisition.Edge.Agent.Services;

/// <summary>
/// 负责解析并持久化稳定的 EdgeId（用于中心注册/心跳）。
/// </summary>
public sealed class EdgeIdentityService
{
    private readonly EdgeReportingOptions _options;
    private readonly ILogger<EdgeIdentityService> _logger;
    private string? _cachedEdgeId;

    public EdgeIdentityService(IOptions<EdgeReportingOptions> options, ILogger<EdgeIdentityService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string GetOrCreateEdgeId()
    {
        if (!string.IsNullOrWhiteSpace(_cachedEdgeId)) return _cachedEdgeId!;

        // 1) 显式配置优先
        if (!string.IsNullOrWhiteSpace(_options.EdgeId))
        {
            _cachedEdgeId = _options.EdgeId!.Trim();
            return _cachedEdgeId!;
        }

        // 2) 环境变量（便于容器/K8s 注入）
        var env = Environment.GetEnvironmentVariable("EDGE_ID");
        if (!string.IsNullOrWhiteSpace(env))
        {
            _cachedEdgeId = env.Trim();
            return _cachedEdgeId!;
        }

        // 3) 本地持久化文件（跨重启保持稳定）
        var path = _options.IdentityFilePath;
        if (!Path.IsPathRooted(path)) path = Path.Combine(AppContext.BaseDirectory, path);

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    _cachedEdgeId = existing;
                    return _cachedEdgeId!;
                }
            }

            var generated = $"edge-{Guid.NewGuid():N}";
            File.WriteAllText(path, generated);
            _cachedEdgeId = generated;
            _logger.LogInformation("已生成并持久化 EdgeId: {EdgeId} -> {Path}", generated, path);
            return _cachedEdgeId!;
        }
        catch (Exception ex)
        {
            // 兜底：即使持久化失败，也生成一个 EdgeId 继续运行（但重启可能变化）
            var fallback = $"edge-{Guid.NewGuid():N}";
            _cachedEdgeId = fallback;
            _logger.LogWarning(ex, "EdgeId 持久化失败，将使用临时 EdgeId: {EdgeId}", fallback);
            return _cachedEdgeId!;
        }
    }
}

