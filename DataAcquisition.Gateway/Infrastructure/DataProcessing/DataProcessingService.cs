using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.Models;
using DataAcquisition.Core.OperationalEvents;

namespace DataAcquisition.Gateway.Infrastructure.DataProcessing;

/// <summary>
/// 数据预处理服务，实现执行表达式并处理异常
/// </summary>
public class DataProcessingService : IDataProcessingService
{
    private readonly IOperationalEvents _events;

    public DataProcessingService(IOperationalEvents events)
    {
        _events = events;
    }

    public async Task<DataMessage> ExecuteAsync(DataMessage dataMessage)
    {
        try
        {
            await ExpressionEvaluator.EvaluateAsync(dataMessage);
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync("System", $"Error handling data point: {ex.Message} - StackTrace: {ex.StackTrace}", ex);
        }

        return dataMessage;
    }
}
