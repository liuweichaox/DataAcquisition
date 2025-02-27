using System.Threading.Tasks;

namespace DataAcquisition.Core.Delegates
{
    /// <summary>
    /// 消息发送委托
    /// </summary>
    /// <param name="message">要发送的消息</param>
    /// <returns>异步任务</returns>
    public delegate Task MessageSendDelegate(string message);
}