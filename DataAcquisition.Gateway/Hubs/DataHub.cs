using Microsoft.AspNetCore.SignalR;

namespace DataAcquisition.Gateway.Hubs;

public class DataHub : Hub
{
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}