using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Edge.Agent.Controllers;

/// <summary>
/// 日志查看页面（UI）
/// </summary>
[Route("logs")]
public sealed class LogsPageController : Controller
{
    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index()
    {
        return View("~/Views/Logs/Index.cshtml");
    }
}

