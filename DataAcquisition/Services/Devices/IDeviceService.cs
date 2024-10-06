using System.Collections.Generic;
using System.Threading.Tasks;
using DataAcquisition.Models;

namespace DataAcquisition.Services.Devices;

/// <summary>
/// 设备服务接口
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// 获取所有设备
    /// </summary>
    /// <returns></returns>
    Task<List<Device>> GetDevices();
}