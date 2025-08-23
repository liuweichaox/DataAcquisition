using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Models;

namespace DataAcquisition.Gateway.DataProcessing;

/// <summary>
/// 数据预处理服务，实现执行表达式并处理异常
/// </summary>
public class DataProcessingService : IDataProcessingService
{
    private readonly IMessage _message;

    public DataProcessingService(IMessage message)
    {
        _message = message;
    }

    public async Task<DataMessage> ExecuteAsync(DataMessage dataMessage)
    {
        try
        {
            await ExpressionEvaluator.EvaluateAsync(dataMessage);
        }
        catch (Exception ex)
        {
            await _message.SendAsync($"Error handling data point: {ex.Message} - StackTrace: {ex.StackTrace}");
        }

        return dataMessage;
    }
}
