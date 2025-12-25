using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Central.Web.Controllers;

/// <summary>
///     日志查看控制器
/// </summary>
public class LogsController : Controller
{
    /// <summary>
    ///     日志查看页面
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}