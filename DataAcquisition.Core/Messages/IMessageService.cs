using System.Threading.Tasks;

namespace DataAcquisition.Core.Messages;

/// <summary>
/// 消息服务
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    Task SendAsync(string message);
}