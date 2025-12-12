using DataAcquisition.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 日志查看控制器
/// </summary>
public class LogsController : Controller
{
    private readonly ILogViewService _logViewService;

    public LogsController(ILogViewService logViewService)
    {
        _logViewService = logViewService;
    }

    /// <summary>
    /// 日志查看页面
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// 获取日志数据 API
    /// </summary>
    [HttpGet]
    [Route("api/logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? level = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var skip = (page - 1) * pageSize;
            var (entries, totalCount) = await _logViewService.GetLogsAsync(
                level, keyword, skip, pageSize, cancellationToken);

            return Ok(new
            {
                Data = entries,
                Total = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// 获取可用的日志级别
    /// </summary>
    [HttpGet]
    [Route("api/logs/levels")]
    public IActionResult GetLevels()
    {
        var levels = _logViewService.GetAvailableLevels();
        return Ok(levels);
    }
}
