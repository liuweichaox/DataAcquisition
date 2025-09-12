using System.Threading.Tasks;
using DataAcquisition.Domain.Models;
using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Infrastructure.DataProcessing;

/// <summary>
/// 默认数据预处理服务，未实现任何处理逻辑。
/// </summary>
public class DataProcessingService : IDataProcessingService
{
    /// <summary>
    /// 直接返回原始数据消息。
    /// </summary>
    public Task<DataMessage> ExecuteAsync(DataMessage dataMessage)
    {
        return Task.FromResult(dataMessage);
    }
}
