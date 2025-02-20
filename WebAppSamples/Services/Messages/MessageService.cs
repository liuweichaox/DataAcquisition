using DataAcquisition.Services.Messages;
using Microsoft.AspNetCore.SignalR;
using WebAppSamples.Hubs;

namespace WebAppSamples.Services.Messages;

public class MessageService(IHubContext<DataHub> hubContext): IMessageService
{
    public async Task SendAsync(string message)
    {
        await hubContext.Clients.All.SendAsync("ReceiveMessage", message);
    }
}