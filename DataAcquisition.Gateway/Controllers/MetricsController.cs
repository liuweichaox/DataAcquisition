using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 指标查看控制器
/// </summary>
[ApiController]
[Route("api/metrics-data")]
public class MetricsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MetricsController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// 获取格式化的指标数据（JSON 格式）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMetricsJson()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            // 使用相对路径，避免硬编码
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            // 使用原始 Prometheus 端点（已改为 /metrics/raw）
            var response = await client.GetStringAsync($"{baseUrl}/metrics/raw");

            var metrics = ParsePrometheusMetrics(response);

            return Ok(new
            {
                // 使用本地时间，避免 UTC 显示
                timestamp = DateTime.Now,
                metrics = metrics
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取指标信息（说明如何查看指标）
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetMetricsInfo()
    {
        return Ok(new
        {
            message = "指标数据查看方式",
            endpoints = new
            {
                json = "/api/metrics-data - JSON 格式的指标数据（推荐）",
                prometheus = "/metrics/raw - Prometheus 原始格式",
                html = "/metrics - HTML 可视化页面"
            },
            availableMetrics = new[]
            {
                "data_acquisition_collection_latency_ms - 采集延迟（毫秒）",
                "data_acquisition_collection_rate - 采集频率（points/s）",
                "data_acquisition_queue_depth - 队列深度（Channel待读取 + 批量积累，消息数）",
                "data_acquisition_processing_latency_ms - 处理延迟（毫秒）",
                "data_acquisition_write_latency_ms - 写入延迟（毫秒）",
                "data_acquisition_batch_write_efficiency - 批量写入效率（points/ms）",
                "data_acquisition_errors_total - 错误总数",
                "data_acquisition_connection_status_changes_total - 连接状态变化总数",
                "data_acquisition_connection_duration_seconds - 连接持续时间（秒）"
            }
        });
    }

    private Dictionary<string, object> ParsePrometheusMetrics(string prometheusText)
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
                    if (match.Success)
                    {
                        currentHelp = match.Groups[2].Value;
                    }
                }
                else if (trimmed.StartsWith("# TYPE"))
                {
                    var match = Regex.Match(trimmed, @"# TYPE\s+(\S+)\s+(\S+)");
                    if (match.Success)
                    {
                        if (currentMetric != null && metricData.Count > 0)
                        {
                            result[currentMetric] = new
                            {
                                type = currentType,
                                help = currentHelp,
                                data = metricData
                            };
                        }
                        currentMetric = match.Groups[1].Value;
                        currentType = match.Groups[2].Value;
                        currentHelp = null;
                        metricData = new List<Dictionary<string, object>>();
                    }
                }
                continue;
            }

            // 解析指标行: metric_name{labels} value
            var metricMatch = Regex.Match(trimmed, @"^([^{]+)(?:\{([^}]+)\})?\s+(.+)$");
            if (metricMatch.Success)
            {
                var metricName = metricMatch.Groups[1].Value;
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
                        if (labelParts.Length == 2)
                        {
                            labels[labelParts[0].Trim()] = labelParts[1].Trim().Trim('"');
                        }
                    }
                    dataPoint["labels"] = labels;
                }

                metricData.Add(dataPoint);
            }
        }

        // 添加最后一个指标
        if (currentMetric != null && metricData.Count > 0)
        {
            result[currentMetric] = new
            {
                type = currentType,
                help = currentHelp,
                data = metricData
            };
        }

        return result;
    }
}
