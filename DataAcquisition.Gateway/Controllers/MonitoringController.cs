using DataAcquisition.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 实时监控控制器
/// </summary>
public class MonitoringController : Controller
{
    private readonly IDataAcquisitionService _dataAcquisitionService;
    private readonly IDeviceConfigService _deviceConfigService;

    public MonitoringController(
        IDataAcquisitionService dataAcquisitionService,
        IDeviceConfigService deviceConfigService)
    {
        _dataAcquisitionService = dataAcquisitionService;
        _deviceConfigService = deviceConfigService;
    }

    /// <summary>
    /// 实时监控页面
    /// </summary>
    public IActionResult Realtime()
    {
        return View();
    }
}

/// <summary>
/// 实时监控API控制器
/// </summary>
[Route("api/Monitoring")]
[ApiController]
public class MonitoringApiController : ControllerBase
{
    private readonly IDataAcquisitionService _dataAcquisitionService;
    private readonly IDeviceConfigService _deviceConfigService;

    public MonitoringApiController(
        IDataAcquisitionService dataAcquisitionService,
        IDeviceConfigService deviceConfigService)
    {
        _dataAcquisitionService = dataAcquisitionService;
        _deviceConfigService = deviceConfigService;
    }

    /// <summary>
    /// 获取所有设备状态概览
    /// </summary>
    [HttpGet("GetDeviceStatus")]
    public IActionResult GetDeviceStatus()
    {
        var connectionStatus = _dataAcquisitionService.GetPlcConnectionStatus();
        return Ok(connectionStatus);
    }

    /// <summary>
    /// 获取设备配置信息（包括通道信息）
    /// </summary>
    [HttpGet("GetDeviceConfigs")]
    public async Task<IActionResult> GetDeviceConfigs()
    {
        var configs = await _deviceConfigService.GetConfigs();
        var connectionStatus = _dataAcquisitionService.GetPlcConnectionStatus();
        var result = configs.Select(c =>
        {
            var channels = c.Channels?.Select(ch => new
            {
                ch.ChannelName,
                ch.Measurement,
                ch.AcquisitionInterval,
                DataPointCount = ch.DataPoints?.Count ?? 0,
                HasConditionalAcquisition = ch.ConditionalAcquisition != null
            }).ToList();
            
            return new
            {
                c.Code,
                c.IsEnabled,
                c.Host,
                c.Port,
                c.Type,
                ConnectionStatus = connectionStatus.TryGetValue(c.Code, out var status) ? status : false,
                Channels = channels ?? new List<object>()
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// 获取指定设备的通道列表
    /// </summary>
    [HttpGet("GetChannels/{deviceCode}")]
    public async Task<IActionResult> GetChannels(string deviceCode)
    {
        var configs = await _deviceConfigService.GetConfigs();
        var config = configs.FirstOrDefault(c => c.Code == deviceCode);

        if (config == null)
        {
            return NotFound($"设备 {deviceCode} 不存在");
        }

        var channels = config.Channels?.Select(ch =>
        {
            var dataPoints = ch.DataPoints?.Select(dp => new
            {
                dp.FieldName,
                dp.Register,
                dp.DataType,
                dp.EvalExpression
            }).ToList();
            
            return new
            {
                ch.ChannelName,
                ch.Measurement,
                ch.AcquisitionInterval,
                DataPoints = dataPoints ?? new List<object>(),
                ConditionalAcquisition = ch.ConditionalAcquisition != null ? new
                {
                    ch.ConditionalAcquisition.Register,
                    ch.ConditionalAcquisition.DataType,
                    Start = ch.ConditionalAcquisition.Start != null ? new
                    {
                        ch.ConditionalAcquisition.Start.TriggerMode,
                        ch.ConditionalAcquisition.Start.TimestampField
                    } : null,
                    End = ch.ConditionalAcquisition.End != null ? new
                    {
                        ch.ConditionalAcquisition.End.TriggerMode,
                        ch.ConditionalAcquisition.End.TimestampField
                    } : null
                } : null
            };
        }).ToList() ?? new List<object>();

        return Ok(channels);
    }
}
