using Microsoft.AspNetCore.SignalR;

namespace WebAppSamples.Hubs;

public class DataHub : Hub
{
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}