using DataAcquisition.Central.Api.Services;
using DataAcquisition.Contracts.Edge;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Central.Api.Controllers;

[ApiController]
[Route("api/edges")]
public class EdgesController(EdgeRegistry registry) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        return Ok(registry.List().OrderByDescending(e => e.LastSeen));
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] EdgeRegistrationRequest request)
    {
        var now = DateTimeOffset.Now;
        var state = registry.Upsert(request.EdgeId, request.AgentBaseUrl, request.Hostname, null, now);
        return Ok(new { state.EdgeId, state.AgentBaseUrl, state.Hostname, state.LastSeen });
    }

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat([FromBody] EdgeHeartbeatRequest request)
    {
        var now = request.Timestamp == default ? DateTimeOffset.Now : request.Timestamp;
        var state = registry.Heartbeat(request.EdgeId, request.AgentBaseUrl, request.BufferBacklog, request.LastError, now);
        return Ok(new { state.EdgeId, state.AgentBaseUrl, state.LastSeen, state.BufferBacklog, state.LastError });
    }
}
