using DataAcquisition.Domain.Models;
using DataAcquisition.Application.DataProcessing;
using DataAcquisition.Application.OperationalEvents;

namespace DataAcquisition.Infrastructure.DataProcessing;

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
