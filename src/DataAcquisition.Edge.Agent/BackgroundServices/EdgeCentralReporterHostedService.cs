using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using DataAcquisition.Contracts.Edge;
using DataAcquisition.Edge.Agent.Services;
using DataAcquisition.Infrastructure.DataStorages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataAcquisition.Edge.Agent.BackgroundServices;

/// <summary>
/// Edge 启动即自动注册到中心，并周期发送心跳（在线、积压、错误摘要等）。
/// </summary>
public sealed class EdgeCentralReporterHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly EdgeIdentityService _identity;
    private readonly ILogger<EdgeCentralReporterHostedService> _logger;
    private readonly EdgeReportingOptions _options;
    private readonly ParquetFileStorageService _parquetStorage;

    private string? _lastError;
    private string? _cachedAgentBaseUrl;

    public EdgeCentralReporterHostedService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<EdgeReportingOptions> options,
        EdgeIdentityService identity,
        ParquetFileStorageService parquetStorage,
        ILogger<EdgeCentralReporterHostedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _identity = identity;
        _parquetStorage = parquetStorage;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableCentralReporting)
        {
            _logger.LogInformation("已禁用中心上报（Edge:EnableCentralReporting=false）");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.CentralApiBaseUrl))
        {
            _logger.LogWarning("CentralApiBaseUrl 为空，跳过中心上报");
            return;
        }

        string edgeId;
        try
        {
            edgeId = _identity.GetEdgeId();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "无法获取 EdgeId，已跳过中心上报");
            return;
        }
        var hostname = Environment.MachineName;
        var version = GetVersion();
        var agentBaseUrl = ResolveAgentBaseUrlOrNull();
        if (string.IsNullOrWhiteSpace(agentBaseUrl))
        {
            _logger.LogWarning("未配置且无法推导 AgentBaseUrl；中心将无法代理 metrics/logs（可在 Edge:AgentBaseUrl 配置显式值）");
        }

        var baseUri = new Uri(_options.CentralApiBaseUrl.TrimEnd('/') + "/");
        var http = _httpClientFactory.CreateClient(nameof(EdgeCentralReporterHostedService));
        http.BaseAddress = baseUri;

        _logger.LogInformation("中心上报启用：EdgeId={EdgeId}, Central={Central}", edgeId, baseUri);

        // 启动即注册：如果中心暂不可用，则持续重试直到成功（或进程退出）
        await RegisterWithRetryAsync(http, edgeId, hostname, version, agentBaseUrl, stoppingToken).ConfigureAwait(false);

        var heartbeatSeconds = _options.HeartbeatIntervalSeconds <= 0 ? 10 : _options.HeartbeatIntervalSeconds;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(heartbeatSeconds));

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            // AgentBaseUrl 可能来自环境变量/配置变更，周期性重新解析一次（低成本）
            agentBaseUrl ??= ResolveAgentBaseUrlOrNull();
            await SendHeartbeatAsync(http, edgeId, agentBaseUrl, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RegisterWithRetryAsync(HttpClient http, string edgeId, string hostname, string? version, string? agentBaseUrl,
        CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        var maxDelay = TimeSpan.FromSeconds(30);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var req = new EdgeRegistrationRequest
                {
                    EdgeId = edgeId,
                    AgentBaseUrl = agentBaseUrl,
                    Hostname = hostname,
                    Version = version
                };

                using var resp = await http.PostAsJsonAsync("api/edges/register", req, JsonOptions, ct)
                    .ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

                _lastError = null;
                _logger.LogInformation("已向中心注册/更新：EdgeId={EdgeId}", edgeId);
                return;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _logger.LogWarning(ex, "中心注册失败（将重试）：{Message}", ex.Message);

                await Task.Delay(delay, ct).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(Math.Min(maxDelay.TotalSeconds, delay.TotalSeconds * 2));
            }
        }
    }

    private async Task SendHeartbeatAsync(HttpClient http, string edgeId, string? agentBaseUrl, CancellationToken ct)
    {
        long? backlog = null;
        try
        {
            var pending = await _parquetStorage.GetPendingFilesAsync().ConfigureAwait(false);
            backlog = pending.Count;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "获取 WAL 积压量失败：{Message}", ex.Message);
        }

        try
        {
            var req = new EdgeHeartbeatRequest
            {
                EdgeId = edgeId,
                AgentBaseUrl = agentBaseUrl,
                BufferBacklog = backlog,
                LastError = _lastError,
                Timestamp = DateTimeOffset.UtcNow
            };

            using var resp = await http.PostAsJsonAsync("api/edges/heartbeat", req, JsonOptions, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            _lastError = null;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogWarning(ex, "中心心跳失败：{Message}", ex.Message);
        }
    }

    private string? ResolveAgentBaseUrlOrNull()
    {
        if (!string.IsNullOrWhiteSpace(_cachedAgentBaseUrl)) return _cachedAgentBaseUrl;

        // 1) 显式配置优先
        var configured = _options.AgentBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _cachedAgentBaseUrl = NormalizeBaseUrl(configured);
            return _cachedAgentBaseUrl;
        }

        // 2) 尝试从监听地址推导（适用于本机/同容器内 central 调用）
        var urls = _configuration["Urls"] ?? _configuration["ASPNETCORE_URLS"];
        if (string.IsNullOrWhiteSpace(urls)) return null;

        // Urls 可能是 "http://localhost:8001;https://localhost:8002"
        var first = Regex.Split(urls, @"[;,]\s*").FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim();
        if (string.IsNullOrWhiteSpace(first)) return null;

        if (!Uri.TryCreate(first, UriKind.Absolute, out var uri)) return null;

        // 通配监听地址无法被中心访问（需要显式配置）
        if (string.Equals(uri.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "[::]", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "*", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "+", StringComparison.OrdinalIgnoreCase))
            return null;

        _cachedAgentBaseUrl = NormalizeBaseUrl($"{uri.Scheme}://{uri.Host}:{uri.Port}");
        return _cachedAgentBaseUrl;
    }

    private static string? NormalizeBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        return trimmed.EndsWith("/") ? trimmed.TrimEnd('/') : trimmed;
    }

    private static string? GetVersion()
    {
        try
        {
            var asm = Assembly.GetEntryAssembly() ?? typeof(EdgeCentralReporterHostedService).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info)) return info;
            return asm.GetName().Version?.ToString();
        }
        catch
        {
            return null;
        }
    }
}

