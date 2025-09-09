using DataAcquisition.Core.DataAcquisitions;
using DataAcquisition.Gateway.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 数据采集控制器
/// </summary>
[Route("api/[controller]/[action]")]
public class DataAcquisitionController: ControllerBase
{
    private readonly IDataAcquisitionService _dataAcquisitionService;
    public DataAcquisitionController(IDataAcquisitionService dataAcquisitionService)
    {
        _dataAcquisitionService = dataAcquisitionService;
    }
    
    /// <summary>
    /// 获取 PLC 连接状态
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public IActionResult GetPlcConnectionStatus()
    {
        var plcConnectionStatus = _dataAcquisitionService.GetPlcConnectionStatus();
        return Ok(plcConnectionStatus);
    }

    /// <summary>
    /// 写入 PLC 寄存器
    /// </summary>
    /// <param name="request">写入请求</param>
    [HttpPost]
    public async Task<IActionResult> WriteRegister([FromBody] PlcWriteRequest request)
    {
        var result = await _dataAcquisitionService.WritePlcAsync(request.PlcCode, request.Address, request.Value, request.DataType);
        if (result.IsSuccess)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }
}