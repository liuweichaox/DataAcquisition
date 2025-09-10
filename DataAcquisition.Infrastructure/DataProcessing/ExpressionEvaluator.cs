using System.Linq;
using System.Threading.Tasks;
using NCalc;
using DataAcquisition.Domain.Models;

namespace DataAcquisition.Infrastructure.DataProcessing;

/// <summary>
/// 提供表达式计算功能
/// </summary>
public static class ExpressionEvaluator
{
    /// <summary>
    /// 对数据消息中的数值字段应用 EvalExpression
    /// </summary>
    /// <param name="dataMessage">待处理的数据消息</param>
    public static async Task EvaluateAsync(DataMessage dataMessage)
    {
        foreach (var kv in dataMessage.DataValues.ToList())
        {
            if (!IsNumberType(kv.Value)) continue;

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
            dataMessage.DataValues[kv.Key] = value ?? 0;
        }
    }
    
    /// <summary>
    /// 判断对象是否为数值类型。
    /// </summary>
    private static bool IsNumberType(object value)
    {
        return value is ushort or uint or ulong or short or int or long or float or double;
    }
}
