using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using System.Collections.Generic;
using System.Linq;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 指标查看控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    /// <summary>
    /// 获取指标信息（说明如何查看指标）
    /// </summary>
    [HttpGet]
    public IActionResult GetMetricsInfo()
    {
        return Ok(new
        {
            message = "指标数据已通过 Prometheus 格式暴露",
            endpoints = new
            {
                prometheus = "/metrics",
                description = "访问 http://localhost:8000/metrics 查看所有指标（Prometheus 格式）"
            },
            availableMetrics = new[]
            {
                "data_acquisition_collection_latency_ms - 采集延迟（毫秒）",
                "data_acquisition_collection_rate - 采集频率（points/s）",
                "data_acquisition_queue_depth - 队列深度（消息数）",
                "data_acquisition_processing_latency_ms - 处理延迟（毫秒）",
                "data_acquisition_write_latency_ms - 写入延迟（毫秒）",
                "data_acquisition_batch_write_efficiency - 批量写入效率（points/ms）",
                "data_acquisition_errors_total - 错误总数",
                "data_acquisition_connection_status_changes_total - 连接状态变化总数",
                "data_acquisition_connection_duration_seconds - 连接持续时间（秒）"
            },
            usage = new
            {
                prometheus = "配置 Prometheus 服务器抓取 /metrics 端点",
                grafana = "使用 Grafana 连接 Prometheus 数据源进行可视化",
                direct = "直接访问 /metrics 端点查看原始指标数据"
            }
        });
    }
}

