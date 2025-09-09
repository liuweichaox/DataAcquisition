using System.Text.Json;
using DataAcquisition.Core.Models;

namespace DataAcquisition.Core.DeviceConfigs;

/// <summary>
/// 提供加载设备配置文件的服务。
/// </summary>
public class DeviceConfigService : IDeviceConfigService
{
    public async Task<List<DeviceConfig>> GetConfigs()
    {
        var dataAcquisitionConfigs = await LoadAllJsonFilesAsync<DeviceConfig>("Configs");
        return dataAcquisitionConfigs;
    }

    /// <summary>
    /// 异步加载 JSON 配置文件
    /// </summary>
    /// <typeparam name="T">要反序列化的目标类型</typeparam>
    /// <param name="filePath">JSON 文件路径</param>
    /// <returns>反序列化后的对象</returns>
    private static async Task<T> LoadConfigAsync<T>(string filePath)
    {
        await using var stream = new FileStream(Path.Combine(AppContext.BaseDirectory, filePath), FileMode.Open,
            FileAccess.Read);
        return await JsonSerializer.DeserializeAsync<T>(stream);
    }

    /// <summary>
    /// 遍历指定文件夹下的所有 JSON 文件，并加载每个文件的内容
    /// </summary>
    /// <typeparam name="T">要反序列化的目标类型</typeparam>
    /// <param name="directoryPath">目录路径</param>
    /// <returns>包含所有 JSON 文件内容的列表</returns>
    private static async Task<List<T>> LoadAllJsonFilesAsync<T>(string directoryPath)
    {
        var results = new List<T>();

        // Retrieve all JSON files.
        var jsonFiles = Directory.GetFiles(directoryPath, "*.json");

        foreach (var filePath in jsonFiles)
        {
            try
            {
                // Load and deserialize each JSON file.
                var config = await LoadConfigAsync<T>(filePath);
                results.Add(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading file {filePath}: {ex.Message}");
            }
        }

        return results;
    }
}