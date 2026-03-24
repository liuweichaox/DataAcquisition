using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.DeviceConfigs;

/// <summary>
///     设备配置目录校验入口。用于命令行校验和离线检查。
/// </summary>
public sealed class DeviceConfigValidationRunner
{
    private readonly DeviceConfigFileLoader _fileLoader = new();

    public async Task<DeviceConfigValidationSummary> ValidateDirectoryAsync(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("配置目录不能为空", nameof(directory));

        var fullPath = Path.GetFullPath(directory);
        if (!Directory.Exists(fullPath))
        {
            return new DeviceConfigValidationSummary(
                fullPath,
                [new DeviceConfigValidationFileResult(fullPath, null, false, ["配置目录不存在"])]);
        }

        var files = Directory.GetFiles(fullPath, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<DeviceConfigValidationFileResult>();
        foreach (var file in files)
            results.Add(await ValidateFileAsync(file).ConfigureAwait(false));

        results = ApplyDuplicatePlcCodeValidation(results);
        return new DeviceConfigValidationSummary(fullPath, results);
    }

    private async Task<DeviceConfigValidationFileResult> ValidateFileAsync(string filePath)
    {
        try
        {
            var config = await _fileLoader.LoadAsync<DeviceConfig>(filePath).ConfigureAwait(false);
            var validation = DeviceConfigValidator.Validate(config);
            return new DeviceConfigValidationFileResult(filePath, config.PlcCode, validation.IsValid, validation.Errors);
        }
        catch (Exception ex)
        {
            return new DeviceConfigValidationFileResult(filePath, null, false, [ex.Message]);
        }
    }

    private static List<DeviceConfigValidationFileResult> ApplyDuplicatePlcCodeValidation(
        List<DeviceConfigValidationFileResult> files)
    {
        var duplicatePlcCodes = files
            .Where(static file => file.IsValid && !string.IsNullOrWhiteSpace(file.PlcCode))
            .GroupBy(static file => file.PlcCode!, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (duplicatePlcCodes.Count == 0)
            return files;

        return files.Select(file =>
            file.PlcCode != null && duplicatePlcCodes.Contains(file.PlcCode)
                ? file with
                {
                    IsValid = false,
                    Errors = file.Errors.Concat([$"存在重复 PlcCode: {file.PlcCode}"]).ToArray()
                }
                : file).ToList();
    }
}

public sealed record DeviceConfigValidationSummary(
    string Directory,
    IReadOnlyList<DeviceConfigValidationFileResult> Files)
{
    public bool IsValid => Files.Count > 0 && Files.All(static file => file.IsValid);
}

public sealed record DeviceConfigValidationFileResult(
    string FilePath,
    string? PlcCode,
    bool IsValid,
    IReadOnlyList<string> Errors);
