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
        // Vue CLI 构建产物（wwwroot/dist）优先作为首页返回；
        // 若尚未构建（开发阶段），则回退到原 MVC View。
        var distIndex = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "dist", "index.html");
        if (System.IO.File.Exists(distIndex)) return PhysicalFile(distIndex, "text/html; charset=utf-8");

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