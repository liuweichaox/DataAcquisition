using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 首页控制器。
/// </summary>
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 首页视图。
    /// </summary>
    public IActionResult Index()
    {
        return View();
    }
}