using System.Diagnostics;
using DataAcquisition.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Web.Controllers;

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