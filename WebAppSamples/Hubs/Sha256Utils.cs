using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace WebAppSamples.Hubs;

public static class Sha256Utils
{
    /// <summary>
    /// 计算字典的 SHA256 哈希值，并返回十六进制字符串表示。
    /// </summary>
    /// <param name="dictionary">输入字典</param>
    /// <returns>SHA256 哈希值的十六进制字符串</returns>
    public static string ComputeSha256HashForDictionary(Dictionary<string, object> dictionary)
    {
        using var sha256 = SHA256.Create();
        // 将字典的键值对按键排序
        var sortedDictionary = dictionary.OrderBy(k => k.Key).ToDictionary(k => k.Key, v => v.Value);

        // 序列化排序后的字典
        var json = JsonConvert.SerializeObject(sortedDictionary);

        // 计算 SHA256 哈希
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));

        // 将字节数组转换为十六进制字符串
        return ConvertToHexString(hashBytes);
    }

    /// <summary>
    /// 将字节数组转换为十六进制字符串。
    /// </summary>
    /// <param name="bytes">字节数组</param>
    /// <returns>十六进制字符串</returns>
    private static string ConvertToHexString(byte[] bytes)
    {
        var sb = new StringBuilder();
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}