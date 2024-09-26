using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.Utils;

namespace DynamicPLCDataCollector.Services;

public class DeviceService : IDeviceService
{
    public async Task<List<Device>> GetDevices()
    {
        var devices = await JsonUtils.LoadConfigAsync<List<Device>>("Configs/devices.json");
        return devices;
    }
}