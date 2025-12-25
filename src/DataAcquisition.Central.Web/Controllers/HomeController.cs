using System.Diagnostics;
using DataAcquisition.Central.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Central.Web.Controllers;

/// <summary>
///     首页控制器。
/// </summary>
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    /// <summary>
    ///     构造函数。
    /// </summary>
    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     首页视图。
    /// </summary>
    public IActionResult Index()
    {
        // 前端已独立部署：中心只提供 API/metrics。
        // 如配置了 Frontend:BaseUrl，则将首页重定向到前端地址。
        var frontendBaseUrl = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Frontend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(frontendBaseUrl)) return Redirect(frontendBaseUrl.TrimEnd('/'));

        return View();
    }

    /// <summary>
    ///     错误页面视图。
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}