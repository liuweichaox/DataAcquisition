namespace DataAcquisition.Models;

/// <summary>
/// 设备
/// </summary>
public class Device
{
    /// <summary>
    /// 设备编码
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// IP地址
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; }
}