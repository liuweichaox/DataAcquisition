using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.PLCClients;

/// <summary>
/// PLC 客户但管理器实现
/// </summary>
public class PLCClientManager : AbstractPLCClientManager
{
    protected override OperationResult<IPLClient> CreatePLCClient(Device device)
    {
        var plcClient = new PLCClient(device.IpAddress, device.Port);

        var connect = plcClient.ConnectServerAsync().Result;
        if (connect.IsSuccess)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {device.Code} 成功！");
            return new OperationResult<IPLClient>(plcClient);
        }
        else
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 连接到设备 {device.Code} 失败：{connect.Message}");
            return new OperationResult<IPLClient>(connect.Message);
        }
    }
}