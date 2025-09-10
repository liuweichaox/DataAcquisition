using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
/// 数据预处理服务接口。
/// </summary>
public interface IDataProcessingService
{
    /// <summary>
    /// 预处理数据。
    /// </summary>
    /// <param name="dataMessage">待处理的数据消息</param>
    /// <returns>返回处理后的数据消息的任务。</returns>
    Task<DataMessage> ExecuteAsync(DataMessage dataMessage);
}