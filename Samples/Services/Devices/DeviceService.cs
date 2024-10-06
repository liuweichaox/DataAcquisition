using DataAcquisition.Models;
using DataAcquisition.Services.Devices;
using Samples.Utils;

namespace Samples.Services.Devices;

public class DeviceService : IDeviceService
{
    public async Task<List<Device>> GetDevices()
    {
        var devices = await JsonUtils.LoadConfigAsync<List<Device>>("Configs/devices.json");
        return devices;
    }
}