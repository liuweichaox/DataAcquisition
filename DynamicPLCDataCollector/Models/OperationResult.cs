namespace DynamicPLCDataCollector.Models;

public class OperationResult<T>
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
    public T Content { get; set; }

    public OperationResult(T content)
    {
        IsSuccess = true;
        Content = content;
    }
    
    public OperationResult(string message)
    {
        IsSuccess = false;
        Message = message;
    }
}
