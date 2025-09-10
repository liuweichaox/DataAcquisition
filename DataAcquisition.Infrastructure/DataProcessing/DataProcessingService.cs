using System;
using System.Threading.Tasks;
using DataAcquisition.Domain.Models;
using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Infrastructure.DataProcessing;

/// <summary>
/// 数据预处理服务，实现执行表达式并处理异常
/// </summary>
public class DataProcessingService(IOperationalEventsService events) : IDataProcessingService
{
    public async Task<DataMessage> ExecuteAsync(DataMessage dataMessage)
    {
        try
        {
            await ExpressionEvaluator.EvaluateAsync(dataMessage);
        }
        catch (Exception ex)
        {
            await events.ErrorAsync("System", $"Error handling data point: {ex.Message} - StackTrace: {ex.StackTrace}", ex);
        }

        return dataMessage;
    }
}
