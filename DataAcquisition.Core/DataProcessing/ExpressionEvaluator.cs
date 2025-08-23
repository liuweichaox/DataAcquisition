using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Core.Utils;
using NCalc;

namespace DataAcquisition.Core.DataProcessing;

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
        foreach (var kv in dataMessage.Values.ToList())
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
