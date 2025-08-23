using System;
using System.Linq;
using HslCommunication.Core.Device;

namespace DataAcquisition.Core.Communication;

/// <summary>
/// 通过反射或依赖注入创建 PLC 驱动实例。
/// </summary>
public class PlcDriverFactory : IPlcDriverFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PlcDriverFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public DeviceTcpNet Create(DeviceConfig config)
    {
        var driverType = Type.GetType(config.DriverType, false);
        driverType ??= AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => typeof(DeviceTcpNet).IsAssignableFrom(t) &&
                                 t.Name.Equals(config.DriverType, StringComparison.OrdinalIgnoreCase));

        if (driverType is null)
        {
            throw new InvalidOperationException($"未找到名为 {config.DriverType} 的 PLC 驱动，请确认已引用对应协议插件。");
        }

        try
        {
            if (_serviceProvider.GetService(driverType) is DeviceTcpNet serviceInstance)
            {
                serviceInstance.IpAddress = config.Host;
                serviceInstance.Port = config.Port;
                serviceInstance.ReceiveTimeOut = 2000;
                serviceInstance.ConnectTimeOut = 2000;
                return serviceInstance;
            }

            var instance = (DeviceTcpNet)Activator.CreateInstance(driverType, config.Host, config.Port)!;
            instance.ReceiveTimeOut = 2000;
            instance.ConnectTimeOut = 2000;
            return instance;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"创建驱动 {config.DriverType} 失败: {ex.Message}", ex);
        }
    }
}

