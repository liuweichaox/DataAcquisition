﻿using DataAcquisition.Core.DataAcquisitions;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 数据采集控制器
/// </summary>
/// <param name="dataAcquisitionService"></param>
[Route("api/[controller]/[action]")]
public class DataAcquisitionController(IDataAcquisitionService dataAcquisitionService) : ControllerBase
{
    /// <summary>
    /// 开始数据采集任务
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public IActionResult StartCollectionTasks()
    {
        dataAcquisitionService.StartCollectionTasks();
        return Ok("开始数据采集任务");
    }

    /// <summary>
    /// 停止数据采集任务
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public IActionResult StopCollectionTasks()
    {
        dataAcquisitionService.StopCollectionTasks();
        return Ok("停止数据采集任务");
    }
    
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
}