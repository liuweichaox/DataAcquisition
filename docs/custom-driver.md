# 自定义协议插件开发指南

为了支持不同厂商的 PLC 协议，系统提供了 `IPlcClient` 接口与反射驱动工厂。通过实现该接口并在配置文件中声明类型，即可扩展新的协议。

## 1. 实现 `IPlcClient`

```csharp
using DataAcquisition.Core.Communication;
using HslCommunication.Core;
using HslCommunication.Core.Device;

public class MyPlcClient : DeviceTcpNet, IPlcClient
{
    public MyPlcClient(string ip, int port) : base(ip, port)
    {
        // 初始化你的客户端
    }

    // 根据需要重写或封装读写方法
}
```

`IPlcClient` 定义了基础的读写与批量操作能力，可作为自定义驱动的开发参考。

## 2. 在配置中引用插件

将编译后的插件程序集放在应用程序可访问的位置，并在设备配置中指定完整的驱动类型名称：

```json
{
  "DriverType": "MyNamespace.MyPlcClient, MyPluginAssembly"
}
```

`PlcDriverFactory` 会通过反射或依赖注入创建该类型的实例；若类型未找到，将抛出友好的异常提醒。

## 3. 依赖注入（可选）

若自定义驱动依赖其他服务，可在应用启动时通过 DI 容器注册，这样 `PlcDriverFactory` 在创建实例时会自动注入所需依赖。

完成以上步骤后，系统即可识别并使用自定义协议驱动进行数据采集。
