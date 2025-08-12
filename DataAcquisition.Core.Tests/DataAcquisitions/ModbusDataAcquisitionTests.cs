using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HslCommunication.Core.Device;
using Xunit;
using DataAcquisition.Core.DataAcquisitions;
using DataAcquisition.Core.DeviceConfigs;
using DataAcquisition.Core.QueueManagers;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Communication;
using HslCommunication.ModBus;

namespace DataAcquisition.Core.Tests.DataAcquisitions;

public class ModbusDataAcquisitionTests : IDisposable
{
    private readonly ModbusTcpServer _server;
    private readonly int _port = 1502;

    public ModbusDataAcquisitionTests()
    {
        _server = new ModbusTcpServer();
        _server.ServerStart(_port);
    }

    [Fact]
    public async Task AlwaysTriggerCollectsData()
    {
        _server.Write("100", (short)123);
        var config = CreateConfig(TriggerMode.Always);
        var deviceConfigService = new StubDeviceConfigService(config);
        var driverFactory = new StubPlcDriverFactory();
        var queueFactory = new StubQueueManagerFactory();
        var messageService = new StubMessageService();
        var service = new DataAcquisitionService(deviceConfigService, driverFactory, queueFactory, messageService);

        await service.StartCollectionTasks();
        await Task.Delay(200);

        var queue = queueFactory.Manager;
        Assert.NotNull(queue);
        Assert.NotEmpty(queue.Messages);
        Assert.Equal(123, queue.Messages[0].Values["value"]);

        await service.StopCollectionTasks();
    }

    [Fact]
    public async Task ValueIncreaseTriggerCollectsOnRise()
    {
        _server.Write("100", (short)1);
        var config = CreateConfig(TriggerMode.ValueIncrease);
        var deviceConfigService = new StubDeviceConfigService(config);
        var driverFactory = new StubPlcDriverFactory();
        var queueFactory = new StubQueueManagerFactory();
        var messageService = new StubMessageService();
        var service = new DataAcquisitionService(deviceConfigService, driverFactory, queueFactory, messageService);

        await service.StartCollectionTasks();
        await Task.Delay(200);
        var queue = queueFactory.Manager;
        queue.Messages.Clear();

        _server.Write("100", (short)2);
        await Task.Delay(200);

        Assert.Single(queue.Messages);
        Assert.Equal(2, queue.Messages[0].Values["value"]);

        await service.StopCollectionTasks();
    }

    [Fact]
    public async Task ValueDecreaseTriggerCollectsOnDrop()
    {
        _server.Write("100", (short)2);
        var config = CreateConfig(TriggerMode.ValueDecrease);
        var deviceConfigService = new StubDeviceConfigService(config);
        var driverFactory = new StubPlcDriverFactory();
        var queueFactory = new StubQueueManagerFactory();
        var messageService = new StubMessageService();
        var service = new DataAcquisitionService(deviceConfigService, driverFactory, queueFactory, messageService);

        await service.StartCollectionTasks();
        await Task.Delay(200);
        var queue = queueFactory.Manager;
        queue.Messages.Clear();

        _server.Write("100", (short)1);
        await Task.Delay(200);

        Assert.Single(queue.Messages);
        Assert.Equal(1, queue.Messages[0].Values["value"]);

        await service.StopCollectionTasks();
    }

    [Fact]
    public async Task RisingEdgeTriggerCollectsOnTransition()
    {
        _server.Write("100", (short)0);
        var config = CreateConfig(TriggerMode.RisingEdge);
        var deviceConfigService = new StubDeviceConfigService(config);
        var driverFactory = new StubPlcDriverFactory();
        var queueFactory = new StubQueueManagerFactory();
        var messageService = new StubMessageService();
        var service = new DataAcquisitionService(deviceConfigService, driverFactory, queueFactory, messageService);

        await service.StartCollectionTasks();
        await Task.Delay(200);
        var queue = queueFactory.Manager;
        queue.Messages.Clear();

        _server.Write("100", (short)1);
        await Task.Delay(200);

        Assert.Single(queue.Messages);
        Assert.Equal(1, queue.Messages[0].Values["value"]);

        await service.StopCollectionTasks();
    }

    [Fact]
    public async Task FallingEdgeTriggerCollectsOnTransition()
    {
        _server.Write("100", (short)1);
        var config = CreateConfig(TriggerMode.FallingEdge);
        var deviceConfigService = new StubDeviceConfigService(config);
        var driverFactory = new StubPlcDriverFactory();
        var queueFactory = new StubQueueManagerFactory();
        var messageService = new StubMessageService();
        var service = new DataAcquisitionService(deviceConfigService, driverFactory, queueFactory, messageService);

        await service.StartCollectionTasks();
        await Task.Delay(200);
        var queue = queueFactory.Manager;
        queue.Messages.Clear();

        _server.Write("100", (short)0);
        await Task.Delay(200);

        Assert.Single(queue.Messages);
        Assert.Equal(0, queue.Messages[0].Values["value"]);

        await service.StopCollectionTasks();
    }

    private DeviceConfig CreateConfig(TriggerMode mode)
    {
        return new DeviceConfig
        {
            IsEnabled = true,
            Code = "P1",
            Host = "127.0.0.1",
            Port = (ushort)_port,
            DriverType = "ModbusTcpNet",
            HeartbeatMonitorRegister = "1",
            HeartbeatPollingInterval = 50,
            Modules = new List<Module>
            {
                new Module
                {
                    ChamberCode = "C1",
                    Trigger = new Trigger { Mode = mode },
                    BatchReadRegister = "100",
                    BatchReadLength = 1,
                    TableName = "tbl",
                    DataPoints = new List<DataPoint>
                    {
                        new DataPoint{ ColumnName = "value", Index = 0, StringByteLength = 0, DataType = "short", Encoding = ""},
                    }
                }
            }
        };
    }

    public void Dispose()
    {
        _server.ServerClose();
    }
}

internal class StubDeviceConfigService : IDeviceConfigService
{
    private readonly DeviceConfig _config;
    public StubDeviceConfigService(DeviceConfig config) => _config = config;
    public Task<List<DeviceConfig>> GetConfigs() => Task.FromResult(new List<DeviceConfig> { _config });
}

internal class StubPlcDriverFactory : IPlcDriverFactory
{
    public DeviceTcpNet Create(DeviceConfig config)
    {
        var client = new ModbusTcpNet(config.Host, config.Port, 1);
        client.ConnectServer();
        return client;
    }
}

internal class StubQueueManagerFactory : IQueueManagerFactory
{
    public StubQueueManager Manager { get; private set; }
    public IQueueManager Create(DeviceConfig config)
    {
        Manager = new StubQueueManager();
        return Manager;
    }
}

internal class StubQueueManager : IQueueManager
{
    public readonly List<DataMessage> Messages = new();
    public void EnqueueData(DataMessage dataMessage) => Messages.Add(dataMessage);
    public void Dispose() { }
}

internal class StubMessageService : IMessageService
{
    public Task SendAsync(string message) => Task.CompletedTask;
}
