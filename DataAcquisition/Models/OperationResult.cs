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

    /// <summary>
    /// 成功
    /// </summary>
    /// <param name="content"></param>
    public OperationResult(T content)
    {
        IsSuccess = true;
        Content = content;
    }
    
    /// <summary>
    /// 失败
    /// </summary>
    /// <param name="message"></param>
    
    public OperationResult(string message)
    {
        IsSuccess = false;
        Message = message;
    }
}
