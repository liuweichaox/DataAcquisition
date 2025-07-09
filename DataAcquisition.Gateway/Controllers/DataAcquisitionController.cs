using DataAcquisition.Core.DataAcquisitions;
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
}