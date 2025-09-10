namespace DataAcquisition.Domain.Clients;

public class PlcReadResult
{
    public bool IsSuccess { get; set; }
    public byte[] Content { get; set; } = System.Array.Empty<byte>();
    public string Message { get; set; } = string.Empty;
}
