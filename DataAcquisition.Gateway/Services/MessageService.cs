using DataAcquisition.Core.Messages;
using DataAcquisition.Gateway.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DataAcquisition.Gateway.Services;

public class MessageService(IHubContext<DataHub> hubContext) : IMessageService
{
    public async Task SendAsync(string message)
    {
        await hubContext.Clients.All.SendAsync("ReceiveMessage", message);
    }
}