using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using DataAcquisition.Core.DataAcquisitions;
using DataAcquisition.Core.DeviceConfigs;
using DataAcquisition.Core.QueueManagers;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Communication;

namespace DataAcquisition.Core.Tests.DataAcquisitions;

public class TriggerTests
{
    private readonly DataAcquisitionService _service;

    public TriggerTests()
    {
        _service = new DataAcquisitionService(new StubDeviceConfigService(), new StubPlcDriverFactory(), new StubQueueManagerFactory(), new StubMessageService());
    }

    [Fact]
    public void ShouldSample_WhenPrevNull_ReturnsTrue()
    {
        foreach (TriggerMode mode in Enum.GetValues(typeof(TriggerMode)))
        {
            Assert.True(_service.ShouldSample(mode, null, 1));
        }
    }

    [Fact]
    public void Always_TriggerReturnsTrue()
    {
        Assert.True(_service.ShouldSample(TriggerMode.Always, 1, 1));
        Assert.True(_service.ShouldSample(TriggerMode.Always, 2, 5));
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, false)]
    public void ValueIncrease_DetectsIncrease(int prev, int curr, bool expected)
    {
        Assert.Equal(expected, _service.ShouldSample(TriggerMode.ValueIncrease, prev, curr));
    }

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, false)]
    public void ValueDecrease_DetectsDecrease(int prev, int curr, bool expected)
    {
        Assert.Equal(expected, _service.ShouldSample(TriggerMode.ValueDecrease, prev, curr));
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    public void RisingEdge_DetectsTransition(int prev, int curr, bool expected)
    {
        Assert.Equal(expected, _service.ShouldSample(TriggerMode.RisingEdge, prev, curr));
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(0, 0, false)]
    public void FallingEdge_DetectsTransition(int prev, int curr, bool expected)
    {
        Assert.Equal(expected, _service.ShouldSample(TriggerMode.FallingEdge, prev, curr));
    }
}

internal class StubDeviceConfigService : IDeviceConfigService
{
    public Task<List<DeviceConfig>> GetConfigs() => Task.FromResult(new List<DeviceConfig>());
}

internal class StubPlcDriverFactory : IPlcDriverFactory
{
    public DeviceTcpNet Create(DeviceConfig config) => null;
}

internal class StubQueueManagerFactory : IQueueManagerFactory
{
    public IQueueManager Create(DeviceConfig config) => new StubQueueManager();
}

internal class StubQueueManager : IQueueManager
{
    public void EnqueueData(DataMessage dataMessage) { }
    public void Dispose() { }
}

internal class StubMessageService : IMessageService
{
    public Task SendAsync(string message) => Task.CompletedTask;
}
