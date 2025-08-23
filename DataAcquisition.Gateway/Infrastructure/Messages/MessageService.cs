using DataAcquisition.Core.Messages;
using DataAcquisition.Gateway.Hubs;
using HslCommunication.LogNet;
using Microsoft.AspNetCore.SignalR;

namespace DataAcquisition.Gateway.Infrastructure.Messages;

public class MessageService : IMessageService
{
    private readonly IHubContext<DataHub> _hubContext;
    private readonly ILogNet _logger;

    public MessageService(IHubContext<DataHub> hubContext)
    {
        _hubContext = hubContext;
        _logger = new LogNetDateTime("Logs", GenerateMode.ByEveryDay);
    }

    public async Task SendAsync(string message)
    {
        var data = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
        _logger.WriteAnyString(data); 
        await _hubContext.Clients.All.SendAsync("ReceiveMessage", data);
    }
}