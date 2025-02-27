namespace DataAcquisition.Core.Models;

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

/// <summary>
/// object 操作返回类
/// </summary>
public class OperationResult : OperationResult<object>
{
    public static OperationResult From<T>(OperationResult<T> result)
    {
        return new OperationResult
        {
            IsSuccess = result.IsSuccess,
            Content = result.Content,
            Message = result.Message
        };
    }
}