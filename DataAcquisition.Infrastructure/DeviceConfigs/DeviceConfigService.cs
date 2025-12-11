using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace DataAcquisition.Infrastructure.DeviceConfigs;

/// <summary>
/// 提供加载设备配置文件的服务，支持配置热更新。
/// </summary>
public class DeviceConfigService : IDeviceConfigService, IDisposable
{
    private readonly string _configDirectory;
    private FileSystemWatcher? _fileWatcher;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private Dictionary<string, DeviceConfig> _cachedConfigs = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 配置变更事件
    /// </summary>
    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    public DeviceConfigService(IConfiguration configuration)
    {
        _configuration = configuration;
        _configDirectory = Path.Combine(AppContext.BaseDirectory, "Configs");
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        InitializeFileWatcher();
    }

    /// <summary>
    /// 初始化文件监听器
    /// </summary>
    private void InitializeFileWatcher()
    {
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }

        _fileWatcher = new FileSystemWatcher(_configDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += OnConfigFileChanged;
        _fileWatcher.Created += OnConfigFileChanged;
        _fileWatcher.Deleted += OnConfigFileDeleted;
        _fileWatcher.Renamed += OnConfigFileRenamed;
    }

    /// <summary>
    /// 配置文件变更处理
    /// </summary>
    private async void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // 延迟处理，避免文件正在写入时读取
        var delayMs = int.TryParse(_configuration["Acquisition:DeviceConfigService:ConfigChangeDetectionDelayMs"], out var delay) ? delay : 500;
        await Task.Delay(delayMs).ConfigureAwait(false);
        await ReloadConfigAsync(e.FullPath).ConfigureAwait(false);
    }

    /// <summary>
    /// 配置文件删除处理
    /// </summary>
    private async void OnConfigFileDeleted(object sender, FileSystemEventArgs e)
    {
        var fileName = Path.GetFileNameWithoutExtension(e.FullPath);
        if (_cachedConfigs.TryGetValue(fileName, out var oldConfig))
        {
            _cachedConfigs.Remove(fileName);
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                ChangeType = ConfigChangeType.Removed,
                DeviceCode = oldConfig.Code,
                OldConfig = oldConfig
            });
        }
    }

    /// <summary>
    /// 配置文件重命名处理
    /// </summary>
    private async void OnConfigFileRenamed(object sender, RenamedEventArgs e)
    {
        // 处理为删除旧文件，创建新文件
        var oldFileName = Path.GetFileNameWithoutExtension(e.OldFullPath);
        if (_cachedConfigs.TryGetValue(oldFileName, out var oldConfig))
        {
            _cachedConfigs.Remove(oldFileName);
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                ChangeType = ConfigChangeType.Removed,
                DeviceCode = oldConfig.Code,
                OldConfig = oldConfig
            });
        }

        var delayMs = int.TryParse(_configuration["Acquisition:DeviceConfigService:ConfigChangeDetectionDelayMs"], out var delay) ? delay : 500;
        await Task.Delay(delayMs).ConfigureAwait(false);
        await ReloadConfigAsync(e.FullPath).ConfigureAwait(false);
    }

    /// <summary>
    /// 重新加载配置文件
    /// </summary>
    private async Task ReloadConfigAsync(string filePath)
    {
        if (!await _reloadLock.WaitAsync(1000).ConfigureAwait(false))
        {
            return; // 如果正在重新加载，跳过
        }

        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var oldConfig = _cachedConfigs.TryGetValue(fileName, out var config) ? config : null;

            if (!File.Exists(filePath))
            {
                return;
            }

            var newConfig = await LoadConfigAsync<DeviceConfig>(filePath).ConfigureAwait(false);
            var validationResult = await ValidateConfigAsync(newConfig).ConfigureAwait(false);

            if (!validationResult.IsValid)
            {
                // 配置验证失败，记录错误但不更新
                Console.WriteLine($"配置验证失败 {fileName}: {string.Join(", ", validationResult.Errors)}");
                return;
            }

            var changeType = oldConfig == null ? ConfigChangeType.Added : ConfigChangeType.Updated;
            _cachedConfigs[fileName] = newConfig;

            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                ChangeType = changeType,
                DeviceCode = newConfig.Code,
                NewConfig = newConfig,
                OldConfig = oldConfig
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"重新加载配置失败 {filePath}: {ex.Message}");
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public async Task<List<DeviceConfig>> GetConfigs()
    {
        if (_cachedConfigs.Count == 0)
        {
            await LoadAllConfigsAsync().ConfigureAwait(false);
        }
        return _cachedConfigs.Values.ToList();
    }

    /// <summary>
    /// 加载所有配置文件
    /// </summary>
    private async Task LoadAllConfigsAsync()
    {
        _cachedConfigs.Clear();
        if (!Directory.Exists(_configDirectory))
        {
            return;
        }

        var jsonFiles = Directory.GetFiles(_configDirectory, "*.json");
        foreach (var filePath in jsonFiles)
        {
            try
            {
                var config = await LoadConfigAsync<DeviceConfig>(filePath).ConfigureAwait(false);
                var validationResult = await ValidateConfigAsync(config).ConfigureAwait(false);

                if (!validationResult.IsValid)
                {
                    Console.WriteLine($"配置验证失败 {filePath}: {string.Join(", ", validationResult.Errors)}");
                    continue;
                }

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (fileName != null)
                {
                    _cachedConfigs[fileName] = config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置文件失败 {filePath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    private string GetConfigFilePath(string deviceCode)
    {
        return Path.Combine(_configDirectory, $"{deviceCode}.json");
    }

    /// <summary>
    /// 异步加载 JSON 配置文件
    /// </summary>
    /// <typeparam name="T">要反序列化的目标类型</typeparam>
    /// <param name="filePath">JSON 文件路径</param>
    /// <returns>反序列化后的对象</returns>
    private async Task<T> LoadConfigAsync<T>(string filePath)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions);
        return result ?? throw new InvalidOperationException($"无法反序列化配置文件: {filePath}");
    }

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public Task<ConfigValidationResult> ValidateConfigAsync(DeviceConfig config)
    {
        var result = new ConfigValidationResult { IsValid = true };

        // 验证设备编码
        if (string.IsNullOrWhiteSpace(config.Code))
        {
            result.IsValid = false;
            result.Errors.Add("设备编码不能为空");
        }

        // 验证IP地址
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            result.IsValid = false;
            result.Errors.Add("IP地址不能为空");
        }
        else if (!System.Net.IPAddress.TryParse(config.Host, out _))
        {
            result.IsValid = false;
            result.Errors.Add($"无效的IP地址: {config.Host}");
        }

        // 验证端口
        if (config.Port == 0)
        {
            result.IsValid = false;
            result.Errors.Add("端口不能为0");
        }

        // 验证心跳检测地址
        if (string.IsNullOrWhiteSpace(config.HeartbeatMonitorRegister))
        {
            result.IsValid = false;
            result.Errors.Add("心跳检测地址不能为空");
        }

        // 验证通道配置
        if (config.Channels == null || config.Channels.Count == 0)
        {
            result.IsValid = false;
            result.Errors.Add("至少需要配置一个采集通道");
        }
        else
        {
            for (int i = 0; i < config.Channels.Count; i++)
            {
                var channel = config.Channels[i];
                if (string.IsNullOrWhiteSpace(channel.Measurement))
                {
                    result.IsValid = false;
                    result.Errors.Add($"通道 {i + 1} 的测量值名称不能为空");
                }

                if (channel.DataPoints == null || channel.DataPoints.Count == 0)
                {
                    result.IsValid = false;
                    result.Errors.Add($"通道 {i + 1} 至少需要配置一个数据点");
                }
            }
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _reloadLock?.Dispose();
    }

    /// <summary>
    /// 遍历指定文件夹下的所有 JSON 文件，并加载每个文件的内容
    /// </summary>
    /// <typeparam name="T">要反序列化的目标类型</typeparam>
    /// <param name="directoryPath">目录路径</param>
    /// <returns>包含所有 JSON 文件内容的列表</returns>
    private async Task<List<T>> LoadAllJsonFilesAsync<T>(string directoryPath)
    {
        var results = new List<T>();

        if (!Directory.Exists(directoryPath))
        {
            return results;
        }

        // 获取所有 JSON 文件。
        var jsonFiles = Directory.GetFiles(directoryPath, "*.json");

        foreach (var filePath in jsonFiles)
        {
            try
            {
                // 加载并反序列化每个 JSON 文件。
                var config = await LoadConfigAsync<T>(filePath).ConfigureAwait(false);
                results.Add(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载文件 {filePath} 时出错: {ex.Message}");
            }
        }

        return results;
    }
}