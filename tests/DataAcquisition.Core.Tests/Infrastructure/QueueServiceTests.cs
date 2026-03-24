using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using DataAcquisition.Infrastructure.Queues;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DataAcquisition.Core.Tests.Infrastructure;

public sealed class QueueServiceTests
{
    [Fact]
    public async Task SubscribeAsync_ShouldQuarantinePoisonMessageAndPreserveHealthyMessages()
    {
        var wal = new FakeWalStorage();
        var primary = new FakePrimaryStorage();
        var queue = new QueueService(
            wal,
            primary,
            NullLogger<QueueService>.Instance,
            Options.Create(new AcquisitionOptions
            {
                QueueService = new QueueServiceOptions
                {
                    FlushIntervalSeconds = 3600
                }
            }),
            new FakeDeviceConfigService(batchSize: 2));

        using var cts = new CancellationTokenSource();
        var subscribeTask = queue.SubscribeAsync(cts.Token);

        var healthy = CreateMessage("PLC01", "CH01", "sensor", false);
        var poison = CreateMessage("PLC01", "CH01", "sensor", true);

        await queue.PublishAsync(healthy);
        await queue.PublishAsync(poison);

        await WaitUntilAsync(() => primary.SavedMessages.Count == 1 && wal.QuarantinedMessages.Count == 1);

        Assert.Single(primary.SavedMessages);
        Assert.Equal("healthy", primary.SavedMessages.Single().DataValues["quality"]);

        Assert.Single(wal.QuarantinedMessages);
        Assert.Equal("poison", wal.QuarantinedMessages.Single().message.DataValues["quality"]);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await subscribeTask);
        await queue.DisposeAsync();
    }

    private static DataMessage CreateMessage(string plcCode, string channelCode, string measurement, bool poison)
    {
        var message = DataMessage.Create(
            Guid.NewGuid().ToString(),
            measurement,
            plcCode,
            channelCode,
            EventType.Data,
            DateTimeOffset.UtcNow);

        message.AddDataValue("quality", poison ? "poison" : "healthy");
        if (poison)
            message.AddDataValue("poison", true);

        return message;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 5000, int delayMs = 50)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;

            await Task.Delay(delayMs);
        }

        throw new TimeoutException("Condition was not satisfied within the allotted time.");
    }

    private sealed class FakeWalStorage : IWalStorageService
    {
        public ConcurrentBag<(DataMessage message, string reason)> QuarantinedMessages { get; } = new();

        public Task<string> WriteAsync(List<DataMessage> messages)
        {
            if (messages.Any(static msg => msg.DataValues.ContainsKey("poison")))
                throw new InvalidOperationException(messages.Count > 1 ? "poison batch" : "poison message");

            return Task.FromResult($"/tmp/{Guid.NewGuid():N}.parquet");
        }

        public Task<List<DataMessage>> ReadAsync(string filePath) => Task.FromResult(new List<DataMessage>());

        public Task DeleteAsync(string filePath) => Task.CompletedTask;

        public Task MoveToRetryAsync(string filePath) => Task.CompletedTask;

        public Task<List<string>> GetRetryFilesAsync() => Task.FromResult(new List<string>());

        public Task QuarantineInvalidAsync(DataMessage message, string reason)
        {
            QuarantinedMessages.Add((message, reason));
            return Task.CompletedTask;
        }
    }

    private sealed class FakePrimaryStorage : IDataStorageService
    {
        public ConcurrentBag<DataMessage> SavedMessages { get; } = new();

        public Task<bool> SaveBatchAsync(List<DataMessage> dataPoints)
        {
            foreach (var message in dataPoints)
                SavedMessages.Add(message);

            return Task.FromResult(true);
        }
    }

    private sealed class FakeDeviceConfigService : IDeviceConfigService
    {
        private readonly int _batchSize;

        public FakeDeviceConfigService(int batchSize)
        {
            _batchSize = batchSize;
        }

        public event EventHandler<ConfigChangedEventArgs>? ConfigChanged
        {
            add { }
            remove { }
        }

        public Task<List<DeviceConfig>> GetConfigs()
        {
            return Task.FromResult(new List<DeviceConfig>
            {
                new()
                {
                    PlcCode = "PLC01",
                    Channels =
                    [
                        new DataAcquisitionChannel
                        {
                            ChannelCode = "CH01",
                            Measurement = "sensor",
                            BatchSize = _batchSize
                        }
                    ]
                }
            });
        }

        public Task<ConfigValidationResult> ValidateConfigAsync(DeviceConfig config)
        {
            return Task.FromResult(new ConfigValidationResult { IsValid = true });
        }
    }
}
