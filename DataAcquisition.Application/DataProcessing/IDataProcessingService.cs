using System.Threading.Tasks;

namespace DataAcquisition.Application.DataProcessing;

public interface IDataProcessingService
{
    /// <summary>
    /// 预处理数据
    /// </summary>
    /// <param name="dataMessage"></param>
    /// <returns></returns>
    Task<DataMessage> ExecuteAsync(DataMessage dataMessage);
}