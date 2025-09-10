namespace DataAcquisition.Domain.Clients;

public class PlcWriteResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}
