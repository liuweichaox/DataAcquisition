using System.Text.RegularExpressions;
using DataAcquisition.Central.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Central.Api.Controllers;

/// <summary>
/// 中心代理：按 edgeId 代理查询 Edge.Agent 的诊断数据（metrics/logs）。
/// 说明：Central.Api 仍然是纯 API，不提供 UI。
/// </summary>
[ApiController]
[Route("api/edges/{edgeId}")]
public sealed class EdgeDiagnosticsController(EdgeRegistry registry, IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet("metrics/raw")]
    public async Task<IActionResult> GetEdgeMetricsRaw([FromRoute] string edgeId, CancellationToken cancellationToken)
    {
        if (!TryGetEdgeBaseUrl(edgeId, out var baseUrl, out var errorResult)) return errorResult!;

        var uri = new Uri(new Uri(baseUrl), "/metrics");
        var client = httpClientFactory.CreateClient();

        using var resp = await client.GetAsync(uri, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode, body);

        return Content(body, "text/plain; version=0.0.4; charset=utf-8");
    }

    [HttpGet("metrics/json")]
    public async Task<IActionResult> GetEdgeMetricsJson([FromRoute] string edgeId, CancellationToken cancellationToken)
    {
        if (!TryGetEdgeBaseUrl(edgeId, out var baseUrl, out var errorResult)) return errorResult!;

        var uri = new Uri(new Uri(baseUrl), "/metrics");
        var client = httpClientFactory.CreateClient();

        using var resp = await client.GetAsync(uri, cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode, text);

        var metrics = ParsePrometheusMetrics(text);
        return Ok(new
        {
            edgeId,
            timestamp = DateTime.Now,
            metrics
        });
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetEdgeLogs(
        [FromRoute] string edgeId,
        [FromQuery] string? level = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetEdgeBaseUrl(edgeId, out var baseUrl, out var errorResult)) return errorResult!;

        var query = new Dictionary<string, string?>
        {
            ["level"] = string.IsNullOrWhiteSpace(level) ? null : level,
            ["keyword"] = string.IsNullOrWhiteSpace(keyword) ? null : keyword,
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        var qs = string.Join("&", query.Where(kv => kv.Value != null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        var path = string.IsNullOrWhiteSpace(qs) ? "/api/logs" : $"/api/logs?{qs}";
        var uri = new Uri(new Uri(baseUrl), path);

        var client = httpClientFactory.CreateClient();
        using var resp = await client.GetAsync(uri, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode, body);

        // 透传 edge 返回的 JSON（保持字段命名一致）
        return Content(body, "application/json; charset=utf-8");
    }

    [HttpGet("logs/levels")]
    public async Task<IActionResult> GetEdgeLogLevels([FromRoute] string edgeId, CancellationToken cancellationToken)
    {
        if (!TryGetEdgeBaseUrl(edgeId, out var baseUrl, out var errorResult)) return errorResult!;

        var uri = new Uri(new Uri(baseUrl), "/api/logs/levels");
        var client = httpClientFactory.CreateClient();
        using var resp = await client.GetAsync(uri, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode, body);

        return Content(body, "application/json; charset=utf-8");
    }

    private bool TryGetEdgeBaseUrl(string edgeId, out string baseUrl, out IActionResult? errorResult)
    {
        baseUrl = "";
        errorResult = null;

        if (string.IsNullOrWhiteSpace(edgeId))
        {
            errorResult = BadRequest(new { error = "edgeId 不能为空。" });
            return false;
        }

        var state = registry.Find(edgeId);
        if (state == null)
        {
            errorResult = NotFound(new { error = "未找到该 edge。请先调用 /api/edges/register 或 /api/edges/heartbeat。" });
            return false;
        }

        if (string.IsNullOrWhiteSpace(state.AgentBaseUrl))
        {
            errorResult = Conflict(new
            {
                error = "该 edge 未上报 AgentBaseUrl，中心无法代理 metrics/logs。",
                hint = "请在 Edge.Agent 配置 Edge:AgentBaseUrl（或确保 Urls/ASPNETCORE_URLS 可推导），并触发 register/heartbeat 更新。"
            });
            return false;
        }

        baseUrl = state.AgentBaseUrl.TrimEnd('/');
        return true;
    }

    private static Dictionary<string, object> ParsePrometheusMetrics(string prometheusText)
    {
        var result = new Dictionary<string, object>();
        var lines = prometheusText.Split('\n');

        string? currentMetric = null;
        string? currentType = null;
        string? currentHelp = null;
        var metricData = new List<Dictionary<string, object>>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
            {
                if (trimmed.StartsWith("# HELP"))
                {
                    var match = Regex.Match(trimmed, @"# HELP\s+(\S+)\s+(.+)");
                    if (match.Success) currentHelp = match.Groups[2].Value;
                }
                else if (trimmed.StartsWith("# TYPE"))
                {
                    var match = Regex.Match(trimmed, @"# TYPE\s+(\S+)\s+(\S+)");
                    if (match.Success)
                    {
                        if (currentMetric != null && metricData.Count > 0)
                            result[currentMetric] = new
                            {
                                type = currentType,
                                help = currentHelp,
                                data = metricData
                            };
                        currentMetric = match.Groups[1].Value;
                        currentType = match.Groups[2].Value;
                        currentHelp = null;
                        metricData = new List<Dictionary<string, object>>();
                    }
                }

                continue;
            }

            // metric_name{labels} value
            var metricMatch = Regex.Match(trimmed, @"^([^{]+)(?:\{([^}]+)\})?\s+(.+)$");
            if (!metricMatch.Success) continue;

            var labelsStr = metricMatch.Groups[2].Value;
            var value = metricMatch.Groups[3].Value;

            var dataPoint = new Dictionary<string, object>
            {
                ["value"] = double.TryParse(value, out var numValue) ? numValue : value
            };

            if (!string.IsNullOrEmpty(labelsStr))
            {
                var labels = new Dictionary<string, string>();
                foreach (var label in labelsStr.Split(','))
                {
                    var labelParts = label.Split('=');
                    if (labelParts.Length == 2) labels[labelParts[0].Trim()] = labelParts[1].Trim().Trim('"');
                }

                dataPoint["labels"] = labels;
            }

            metricData.Add(dataPoint);
        }

        if (currentMetric != null && metricData.Count > 0)
            result[currentMetric] = new
            {
                type = currentType,
                help = currentHelp,
                data = metricData
            };

        return result;
    }
}

