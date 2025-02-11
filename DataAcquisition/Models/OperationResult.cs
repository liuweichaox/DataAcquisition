namespace DataAcquisition.Models;

/// <summary>
/// 操作返回类
/// </summary>
/// <typeparam name="T"></typeparam>
public class OperationResult<T>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 数据
    /// </summary>
    public T Content { get; set; }
}


public class OperationResult : OperationResult<object>
{
    public static OperationResult<object> From<T>(OperationResult<T> result)
    {
        return new OperationResult<object>
        {
            IsSuccess = result.IsSuccess,
            Content = result.Content,
            Message = result.Message
        };
    }
}