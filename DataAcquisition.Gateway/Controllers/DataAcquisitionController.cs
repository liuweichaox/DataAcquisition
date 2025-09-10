using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Clients;
using DataAcquisition.Gateway.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 数据采集控制器
/// </summary>
[Route("api/[controller]/[action]")]
public class DataAcquisitionController(IDataAcquisitionService dataAcquisitionService) : ControllerBase
{
    /// <summary>
    /// 获取 PLC 连接状态
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public IActionResult GetPlcConnectionStatus()
    {
        var plcConnectionStatus = dataAcquisitionService.GetPlcConnectionStatus();
        return Ok(plcConnectionStatus);
    }

    /// <summary>
    /// 写入 PLC 寄存器
    /// </summary>
    /// <param name="request">写入请求</param>
    [HttpPost]
    public async Task<IActionResult> WriteRegister([FromBody] PlcWriteRequest request)
    {
        var results = new List<PlcWriteResult>();
        var allSuccess = true;

        foreach (var item in request.Items)
        {
            var result = await dataAcquisitionService.WritePlcAsync(request.PlcCode, item.Address, item.Value, item.DataType);
            results.Add(result);
            if (!result.IsSuccess)
            {
                allSuccess = false;
            }
        }

        if (allSuccess)
        {
            return Ok(results);
        }

        return BadRequest(results);
    }
}