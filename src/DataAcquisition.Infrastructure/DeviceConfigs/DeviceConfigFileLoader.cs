using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DataAcquisition.Infrastructure.DeviceConfigs;

internal sealed class DeviceConfigFileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public async Task<T> LoadAsync<T>(string filePath)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"无法反序列化配置文件: {filePath}");
    }
}
