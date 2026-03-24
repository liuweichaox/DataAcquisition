using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataAcquisition.Infrastructure.DeviceConfigs;

/// <summary>
///     提供加载设备配置文件的服务，支持配置热更新。
/// </summary>
public class DeviceConfigService : IDeviceConfigService, IDisposable
{
    private readonly ConcurrentDictionary<string, DeviceConfig> _cachedConfigs = new();
    private readonly string _configDirectory;
    private readonly int _configChangeDetectionDelayMs;
    private readonly DeviceConfigFileLoader _fileLoader;
    private readonly ILogger<DeviceConfigService> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private FileSystemWatcher? _fileWatcher;

    public DeviceConfigService(IOptions<AcquisitionOptions> acquisitionOptions, ILogger<DeviceConfigService> logger)
    {
        _logger = logger;
        _configChangeDetectionDelayMs = acquisitionOptions.Value.DeviceConfigService.ConfigChangeDetectionDelayMs;
        _configDirectory = DeviceConfigPathResolver.Resolve(acquisitionOptions.Value.DeviceConfigService.ConfigDirectory);
        _fileLoader = new DeviceConfigFileLoader();
        InitializeFileWatcher();
    }

    /// <summary>
    ///     配置变更事件
    /// </summary>
    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    public async Task<List<DeviceConfig>> GetConfigs()
    {
        if (_cachedConfigs.IsEmpty) await LoadAllConfigsAsync().ConfigureAwait(false);
        return _cachedConfigs.Values.ToList();
    }

    /// <summary>
    ///     验证配置是否有效
    /// </summary>
    public Task<ConfigValidationResult> ValidateConfigAsync(DeviceConfig config)
        => Task.FromResult(DeviceConfigValidator.Validate(config));

    /// <summary>
    ///     释放资源
    /// </summary>
    public void Dispose()
    {
        _fileWatcher?.Dispose();
        _reloadLock?.Dispose();
    }

    /// <summary>
    ///     初始化文件监听器
    /// </summary>
    private void InitializeFileWatcher()
    {
        if (!Directory.Exists(_configDirectory)) Directory.CreateDirectory(_configDirectory);

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
    ///     配置文件变更处理
    /// </summary>
    private void OnConfigFileChanged(object sender, FileSystemEventArgs e) =>
        _ = HandleConfigFileChangedAsync(e.FullPath);

    /// <summary>
    ///     配置文件删除处理
    /// </summary>
    private void OnConfigFileDeleted(object sender, FileSystemEventArgs e)
    {
        var fileName = Path.GetFileNameWithoutExtension(e.FullPath);
        if (_cachedConfigs.TryRemove(fileName, out var oldConfig))
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                ChangeType = ConfigChangeType.Removed,
                PlcCode = oldConfig.PlcCode,
                OldConfig = oldConfig
            });
    }

    /// <summary>
    ///     配置文件重命名处理
    /// </summary>
    private void OnConfigFileRenamed(object sender, RenamedEventArgs e) =>
        _ = HandleConfigFileRenamedAsync(e);

    private async Task HandleConfigFileChangedAsync(string filePath)
    {
        try
        {
            await Task.Delay(_configChangeDetectionDelayMs).ConfigureAwait(false);
            await ReloadConfigAsync(filePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理配置文件变更失败: {FilePath}", filePath);
        }
    }

    private async Task HandleConfigFileRenamedAsync(RenamedEventArgs e)
    {
        try
        {
            var oldFileName = Path.GetFileNameWithoutExtension(e.OldFullPath);
            if (_cachedConfigs.TryRemove(oldFileName, out var oldConfig))
                ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
                {
                    ChangeType = ConfigChangeType.Removed,
                    PlcCode = oldConfig.PlcCode,
                    OldConfig = oldConfig
                });

            await Task.Delay(_configChangeDetectionDelayMs).ConfigureAwait(false);
            await ReloadConfigAsync(e.FullPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理配置文件重命名失败: {FilePath}", e.FullPath);
        }
    }

    /// <summary>
    ///     重新加载配置文件
    /// </summary>
    private async Task ReloadConfigAsync(string filePath)
    {
        if (!await _reloadLock.WaitAsync(1000).ConfigureAwait(false)) return; // 如果正在重新加载，跳过

        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var oldConfig = _cachedConfigs.GetValueOrDefault(fileName);

            if (!File.Exists(filePath)) return;

            var newConfig = await _fileLoader.LoadAsync<DeviceConfig>(filePath).ConfigureAwait(false);
            var validationResult = await ValidateConfigAsync(newConfig).ConfigureAwait(false);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("配置验证失败 {FileName}: {Errors}", fileName, string.Join(", ", validationResult.Errors));
                return;
            }

            if (TryFindDuplicatePlcCode(fileName, newConfig.PlcCode, out var duplicateFileName))
            {
                _logger.LogWarning(
                    "检测到重复 PlcCode，已拒绝加载配置: PlcCode={PlcCode}, File={FileName}, DuplicateFile={DuplicateFileName}",
                    newConfig.PlcCode,
                    fileName,
                    duplicateFileName);
                return;
            }

            var changeType = oldConfig == null ? ConfigChangeType.Added : ConfigChangeType.Updated;
            _cachedConfigs[fileName] = newConfig;

            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                ChangeType = changeType,
                PlcCode = newConfig.PlcCode,
                NewConfig = newConfig,
                OldConfig = oldConfig
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新加载配置失败 {FilePath}", filePath);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    /// <summary>
    ///     加载所有配置文件
    /// </summary>
    private async Task LoadAllConfigsAsync()
    {
        _cachedConfigs.Clear();
        if (!Directory.Exists(_configDirectory)) return;

        var jsonFiles = Directory.GetFiles(_configDirectory, "*.json");
        foreach (var filePath in jsonFiles)
            try
            {
                var config = await _fileLoader.LoadAsync<DeviceConfig>(filePath).ConfigureAwait(false);
                var validationResult = await ValidateConfigAsync(config).ConfigureAwait(false);

                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("配置验证失败 {FilePath}: {Errors}", filePath, string.Join(", ", validationResult.Errors));
                    continue;
                }

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (TryFindDuplicatePlcCode(fileName, config.PlcCode, out var duplicateFileName))
                {
                    _logger.LogWarning(
                        "检测到重复 PlcCode，已跳过配置文件: PlcCode={PlcCode}, File={FileName}, DuplicateFile={DuplicateFileName}",
                        config.PlcCode,
                        fileName,
                        duplicateFileName);
                    continue;
                }

                _cachedConfigs[fileName] = config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载配置文件失败 {FilePath}", filePath);
            }
    }

    private bool TryFindDuplicatePlcCode(string fileName, string plcCode, out string duplicateFileName)
    {
        if (string.IsNullOrWhiteSpace(plcCode))
        {
            duplicateFileName = string.Empty;
            return false;
        }

        foreach (var cachedConfig in _cachedConfigs)
        {
            if (string.Equals(cachedConfig.Key, fileName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(cachedConfig.Value.PlcCode, plcCode, StringComparison.OrdinalIgnoreCase))
                continue;

            duplicateFileName = cachedConfig.Key;
            return true;
        }

        duplicateFileName = string.Empty;
        return false;
    }
}
