using DataAcquisition.Services.Messages;
using DataAcquisitionGateway.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DataAcquisitionGateway.Services.Messages;

public class MessageService(IHubContext<DataHub> hubContext) : IMessageService
{
    public async Task SendAsync(string message)
    {
        await hubContext.Clients.All.SendAsync("ReceiveMessage", message);
    }
}