using Microsoft.AspNetCore.SignalR;

namespace DataAcquisition.Gateway.Hubs
{
    /// <summary>
    /// 数据推送 Hub，支持分组订阅。
    /// </summary>
    public class DataHub : Hub
    {
        /// <summary>
        /// 订阅指定设备的事件和数据
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        public async Task SubscribeToDevice(string deviceCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceCode}");
            await Clients.Caller.SendAsync("Subscribed", $"已订阅设备: {deviceCode}");
        }

        /// <summary>
        /// 取消订阅指定设备
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        public async Task UnsubscribeFromDevice(string deviceCode)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device:{deviceCode}");
            await Clients.Caller.SendAsync("Unsubscribed", $"已取消订阅设备: {deviceCode}");
        }

        /// <summary>
        /// 订阅指定通道的事件和数据
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="channelName">通道名称</param>
        public async Task SubscribeToChannel(string deviceCode, string channelName)
        {
            var groupName = $"channel:{deviceCode}:{channelName}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Clients.Caller.SendAsync("Subscribed", $"已订阅通道: {deviceCode}/{channelName}");
        }

        /// <summary>
        /// 取消订阅指定通道
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="channelName">通道名称</param>
        public async Task UnsubscribeFromChannel(string deviceCode, string channelName)
        {
            var groupName = $"channel:{deviceCode}:{channelName}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Caller.SendAsync("Unsubscribed", $"已取消订阅通道: {deviceCode}/{channelName}");
        }
    }
}