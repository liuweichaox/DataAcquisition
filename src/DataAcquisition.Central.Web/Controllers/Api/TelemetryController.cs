using DataAcquisition.Contracts.Telemetry;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Central.Web.Controllers.Api;

[ApiController]
[Route("api/telemetry")]
public class TelemetryController : ControllerBase
{
    [HttpPost("ingest")]
    public IActionResult Ingest([FromBody] TelemetryBatchRequest request)
    {
        // 第一阶段：只打通边缘->中心的数据通路，先不落库（后续接 Central.Application/Infrastructure）。
        if (request.Points.Count == 0)
            return BadRequest(new { error = "Points 不能为空" });

        return Ok(new
        {
            request.EdgeId,
            request.BatchId,
            received = request.Points.Count
        });
    }
}

