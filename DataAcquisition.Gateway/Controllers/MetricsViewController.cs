using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 指标可视化控制器
/// </summary>
public class MetricsViewController : Controller
{
    /// <summary>
    /// 显示指标可视化页面
    /// </summary>
    [HttpGet("/metrics")]
    public IActionResult Index()
    {
        // 显式指定视图路径，避免查找错误
        return View("~/Views/Metrics/View.cshtml");
    }
}
