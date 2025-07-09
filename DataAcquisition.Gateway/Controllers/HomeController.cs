using DataAcquisition.Core.Utils;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }
}