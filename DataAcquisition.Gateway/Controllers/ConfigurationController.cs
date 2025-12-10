using System.Text.Json;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataAcquisition.Gateway.Controllers;

/// <summary>
/// 设备配置管理控制器
/// </summary>
public class ConfigurationController : Controller
{
    private readonly IDeviceConfigService _deviceConfigService;

    public ConfigurationController(IDeviceConfigService deviceConfigService)
    {
        _deviceConfigService = deviceConfigService;
    }

    /// <summary>
    /// 设备配置管理页面
    /// </summary>
    public IActionResult Devices()
    {
        return View();
    }
}

/// <summary>
/// 设备配置管理API控制器
/// </summary>
[Route("api/Configuration")]
[ApiController]
public class ConfigurationApiController : ControllerBase
{
    private readonly IDeviceConfigService _deviceConfigService;
    private readonly IDataAcquisitionService _dataAcquisitionService;
    private readonly ILogger<ConfigurationApiController> _logger;
    private static readonly string ConfigsPath = Path.Combine(AppContext.BaseDirectory, "Configs");

    public ConfigurationApiController(
        IDeviceConfigService deviceConfigService,
        IDataAcquisitionService dataAcquisitionService,
        ILogger<ConfigurationApiController> logger)
    {
        _deviceConfigService = deviceConfigService;
        _dataAcquisitionService = dataAcquisitionService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有设备配置
    /// </summary>
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        var configs = await _deviceConfigService.GetConfigs();
        return Ok(configs);
    }

    /// <summary>
    /// 获取指定设备配置
    /// </summary>
    [HttpGet("Get/{deviceCode}")]
    public async Task<IActionResult> Get(string deviceCode)
    {
        var configs = await _deviceConfigService.GetConfigs();
        var config = configs.FirstOrDefault(c => c.Code == deviceCode);

        if (config == null)
        {
            return NotFound($"设备 {deviceCode} 不存在");
        }

        return Ok(config);
    }

    /// <summary>
    /// 创建新设备配置
    /// </summary>
    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] DeviceConfig config)
    {
        if (string.IsNullOrEmpty(config.Code))
        {
            return BadRequest("设备编码不能为空");
        }

        var configs = await _deviceConfigService.GetConfigs();
        if (configs.Any(c => c.Code == config.Code))
        {
            return BadRequest($"设备 {config.Code} 已存在");
        }

        try
        {
            var filePath = Path.Combine(ConfigsPath, $"{config.Code}.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(stream, config, options);

            _logger.LogInformation($"创建设备配置: {config.Code}");
            return Ok(new { Message = "配置创建成功", DeviceCode = config.Code });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"创建设备配置失败: {config.Code}");
            return StatusCode(500, $"创建配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新设备配置
    /// </summary>
    [HttpPut("Update/{deviceCode}")]
    public async Task<IActionResult> Update(string deviceCode, [FromBody] DeviceConfig config)
    {
        if (config.Code != deviceCode)
        {
            return BadRequest("设备编码不匹配");
        }

        var configs = await _deviceConfigService.GetConfigs();
        if (!configs.Any(c => c.Code == deviceCode))
        {
            return NotFound($"设备 {deviceCode} 不存在");
        }

        try
        {
            var filePath = Path.Combine(ConfigsPath, $"{deviceCode}.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(stream, config, options);

            _logger.LogInformation($"更新设备配置: {deviceCode}");
            return Ok(new { Message = "配置更新成功", DeviceCode = deviceCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"更新设备配置失败: {deviceCode}");
            return StatusCode(500, $"更新配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除设备配置
    /// </summary>
    [HttpDelete("Delete/{deviceCode}")]
    public async Task<IActionResult> Delete(string deviceCode)
    {
        var configs = await _deviceConfigService.GetConfigs();
        if (!configs.Any(c => c.Code == deviceCode))
        {
            return NotFound($"设备 {deviceCode} 不存在");
        }

        try
        {
            var filePath = Path.Combine(ConfigsPath, $"{deviceCode}.json");
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                _logger.LogInformation($"删除设备配置: {deviceCode}");
                return Ok(new { Message = "配置删除成功", DeviceCode = deviceCode });
            }

            return NotFound($"配置文件不存在: {deviceCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"删除设备配置失败: {deviceCode}");
            return StatusCode(500, $"删除配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    [HttpPost("Validate")]
    public IActionResult Validate([FromBody] DeviceConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(config.Code))
        {
            errors.Add("设备编码不能为空");
        }

        if (string.IsNullOrEmpty(config.Host))
        {
            errors.Add("设备IP地址不能为空");
        }

        if (config.Port == 0)
        {
            errors.Add("设备端口不能为0");
        }

        if (config.Channels == null || config.Channels.Count == 0)
        {
            errors.Add("至少需要配置一个采集通道");
        }
        else
        {
            foreach (var channel in config.Channels)
            {
                if (string.IsNullOrEmpty(channel.ChannelName))
                {
                    errors.Add("通道名称不能为空");
                }

                if (string.IsNullOrEmpty(channel.Measurement))
                {
                    errors.Add($"通道 {channel.ChannelName} 的测量值名称不能为空");
                }

                if (channel.DataPoints == null || channel.DataPoints.Count == 0)
                {
                    errors.Add($"通道 {channel.ChannelName} 至少需要配置一个数据点");
                }
            }
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { Errors = errors });
        }

        return Ok(new { Message = "配置验证通过" });
    }
}
