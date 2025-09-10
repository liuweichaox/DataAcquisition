namespace DataAcquisition.Gateway.Models;

/// <summary>
/// 错误信息视图模型。
/// </summary>
public class ErrorViewModel
{
    /// <summary>
    /// 请求标识。
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// 是否显示请求标识。
    /// </summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}