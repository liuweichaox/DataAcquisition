using System;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using NCalc;

namespace DataAcquisition.Core.DataProcessing;

public class DataProcessingService : IDataProcessingService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IMessageService _messageService;
    private readonly IConfiguration _configuration;

    public DataProcessingService(IMemoryCache memoryCache, IMessageService messageService, IConfiguration configuration)
    {
        _memoryCache = memoryCache;
        _messageService = messageService;
        _configuration = configuration;
    }

    /// <summary>
    /// 预处理数据
    /// </summary>
    /// <param name="dataMessage"></param>
    public async Task<DataMessage> ExecuteAsync(DataMessage dataMessage)
    {
        try
        {
            await ApplyEvalExpressionAsync(dataMessage);
        }
        catch (Exception ex)
        {
            await _messageService.SendAsync($"Error handling data point: {ex.Message} - StackTrace: {ex.StackTrace}");
        }

        return dataMessage;
    }

    private async Task ApplyEvalExpressionAsync(DataMessage dataMessage)
    {
        foreach (var kv in dataMessage.Values)
        {
            if (!DataTypeUtils.IsNumberType(kv.Value)) continue;

            var register = dataMessage.DataPoints.SingleOrDefault(x => x.ColumnName == kv.Key);
            if (register == null || string.IsNullOrWhiteSpace(register.EvalExpression) || kv.Value == null) continue;
            var expression = new AsyncExpression(register.EvalExpression)
            {
                Parameters =
                {
                    ["value"] = kv.Value
                }
            };

            var value = await expression.EvaluateAsync();
            dataMessage.Values[kv.Key] = value ?? 0;
        }
    }
}
