using System.Net.Http.Json;
using System.Text.Json;
using DataAcquisition.Contracts.Edge;
using DataAcquisition.Edge.Agent.Services;
using DataAcquisition.Infrastructure.DataStorages;
using Microsoft.Extensions.Configuration;
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
    private readonly EdgeIdentityService _identity;
    private readonly ILogger<EdgeCentralReporterHostedService> _logger;
    private readonly EdgeReportingOptions _options;
    private readonly ParquetFileStorageService _parquetStorage;
    private readonly IConfiguration _configuration;

    private string? _lastError;
    private string? _agentBaseUrl;

    public EdgeCentralReporterHostedService(
        IHttpClientFactory httpClientFactory,
        IOptions<EdgeReportingOptions> options,
        EdgeIdentityService identity,
        ParquetFileStorageService parquetStorage,
        IConfiguration configuration,
        ILogger<EdgeCentralReporterHostedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _identity = identity;
        _parquetStorage = parquetStorage;
        _configuration = configuration;
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

        // 获取 Edge Agent 的 URL（用于中心代理访问）
        var urls = _configuration["Urls"] ?? _configuration["ASPNETCORE_URLS"] ?? "http://localhost:8001";
        var firstUrl = urls.Split(';', ',').FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(firstUrl))
        {
            _agentBaseUrl = firstUrl.TrimEnd('/');
        }

        var baseUri = new Uri(_options.CentralApiBaseUrl.TrimEnd('/') + "/");
        var http = _httpClientFactory.CreateClient(nameof(EdgeCentralReporterHostedService));
        http.BaseAddress = baseUri;

        _logger.LogInformation("中心上报启用：EdgeId={EdgeId}, Central={Central}, AgentBaseUrl={AgentBaseUrl}",
            edgeId, baseUri, _agentBaseUrl);

        // 启动即注册：如果中心暂不可用，则持续重试直到成功（或进程退出）
        await RegisterWithRetryAsync(http, edgeId, hostname, stoppingToken).ConfigureAwait(false);

        var heartbeatSeconds = _options.HeartbeatIntervalSeconds <= 0 ? 10 : _options.HeartbeatIntervalSeconds;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(heartbeatSeconds));

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SendHeartbeatAsync(http, edgeId, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RegisterWithRetryAsync(HttpClient http, string edgeId, string hostname,
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
                    AgentBaseUrl = _agentBaseUrl,
                    Hostname = hostname
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

    private async Task SendHeartbeatAsync(HttpClient http, string edgeId, CancellationToken ct)
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
                AgentBaseUrl = _agentBaseUrl,
                BufferBacklog = backlog,
                LastError = _lastError,
                Timestamp = DateTimeOffset.Now
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
}
