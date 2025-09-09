using System.Text.Json;

namespace DataAcquisition.Gateway.Models;

public class PlcWriteRequest
{
    public string PlcCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
}

