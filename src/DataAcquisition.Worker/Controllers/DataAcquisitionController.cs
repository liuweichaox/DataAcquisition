using DataAcquisition.Application.Commands;
using DataAcquisition.Application.Queries;
using DataAcquisition.Contracts;
using DataAcquisition.Domain.Clients;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Worker.Controllers;

/// <summary>
///     数据采集控制器
/// </summary>
[Route("api/[controller]/[action]")]
public class DataAcquisitionController(IMediator mediator) : ControllerBase
{
    /// <summary>
    ///     获取 PLC 连接状态
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetPlcConnectionStatus(CancellationToken ct)
    {
        var plcConnectionStatus = await mediator.Send(new GetPlcConnectionStatusQuery(), ct);
        return Ok(plcConnectionStatus);
    }

    /// <summary>
    ///     写入 PLC 寄存器
    /// </summary>
    /// <param name="request">写入请求</param>
    [HttpPost]
    public async Task<IActionResult> WriteRegister([FromBody] PLCWriteRequest? request)
    {
        // 输入验证
        if (request == null) return BadRequest(new { error = "请求体不能为空" });

        if (string.IsNullOrWhiteSpace(request.PLCCode)) return BadRequest(new { error = "PLC编码不能为空" });

        if (request.Items.Count == 0) return BadRequest(new { error = "写入项列表不能为空" });

        // 验证每个写入项
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Address)) return BadRequest(new { error = "寄存器地址不能为空" });
            if (string.IsNullOrWhiteSpace(item.DataType)) return BadRequest(new { error = "数据类型不能为空" });
            if (item.Value == null) return BadRequest(new { error = "写入值不能为空" });
        }

        var command = new WritePlcRegisterCommand(
            request.PLCCode,
            request.Items
                .Select(i => new WritePlcRegisterItem(i.Address, i.DataType, i.Value!))
                .ToList());

        var results = await mediator.Send(command);
        var allSuccess = results.All(r => r.IsSuccess);

        if (allSuccess) return Ok(results);

        return BadRequest(results);
    }
}