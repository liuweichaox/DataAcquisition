using DataAcquisition.Central.Api.Services;
using DataAcquisition.Contracts.Edge;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Central.Api.Controllers.Api;

[ApiController]
[Route("api/edges")]
public class EdgesController(EdgeRegistry registry) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        return Ok(registry.List().OrderByDescending(e => e.LastSeenUtc));
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] EdgeRegistrationRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var state = registry.Upsert(request.EdgeId, request.Hostname, request.Version, now);
        return Ok(new { state.EdgeId, state.Hostname, state.Version, state.LastSeenUtc });
    }

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat([FromBody] EdgeHeartbeatRequest request)
    {
        var now = request.Timestamp == default ? DateTimeOffset.UtcNow : request.Timestamp.ToUniversalTime();
        var state = registry.Heartbeat(request.EdgeId, request.BufferBacklog, request.LastError, now);
        return Ok(new { state.EdgeId, state.LastSeenUtc, state.BufferBacklog, state.LastError });
    }
}

