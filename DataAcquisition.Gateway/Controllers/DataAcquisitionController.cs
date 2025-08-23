using DataAcquisition.Core.DataAcquisitions;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 数据采集控制器
/// </summary>
[Route("api/[controller]/[action]")]
public class DataAcquisitionController: ControllerBase
{
    private readonly IDataAcquisition _dataAcquisition;
    public DataAcquisitionController(IDataAcquisition dataAcquisition)
    {
        _dataAcquisition = dataAcquisition;
    }
    
    /// <summary>
    /// 获取 PLC 连接状态
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public IActionResult GetPlcConnectionStatus()
    {
        var plcConnectionStatus = _dataAcquisition.GetPlcConnectionStatus();
        return Ok(plcConnectionStatus);
    }
}