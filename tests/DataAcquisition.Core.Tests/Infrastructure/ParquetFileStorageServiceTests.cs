using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DataAcquisition.Domain.Models;
using DataAcquisition.Infrastructure.DataStorages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DataAcquisition.Core.Tests.Infrastructure;

public sealed class ParquetFileStorageServiceTests
{
    [Fact]
    public async Task ReadAsync_ShouldRestoreScalarTypesFromWal()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Parquet:Directory"] = directory
                })
                .Build();

            using var storage = new ParquetFileStorageService(configuration, NullLogger<ParquetFileStorageService>.Instance);
            var message = DataMessage.Create(
                Guid.NewGuid().ToString(),
                "sensor",
                "PLC01",
                "CH01",
                EventType.Data,
                DateTimeOffset.UtcNow);

            message.AddDataValue("count", 42);
            message.AddDataValue("temperature", 12.5);
            message.AddDataValue("quality", "healthy");
            message.AddDataValue("enabled", true);

            var filePath = await storage.WriteAsync([message]);
            var restoredMessages = await storage.ReadAsync(filePath);
            var restored = Assert.Single(restoredMessages);

            Assert.IsNotType<JsonElement>(restored.DataValues["count"]);
            Assert.IsType<long>(restored.DataValues["count"]);
            Assert.Equal(42L, restored.DataValues["count"]);

            Assert.IsNotType<JsonElement>(restored.DataValues["temperature"]);
            Assert.IsType<decimal>(restored.DataValues["temperature"]);
            Assert.Equal(12.5m, restored.DataValues["temperature"]);

            Assert.IsType<string>(restored.DataValues["quality"]);
            Assert.Equal("healthy", restored.DataValues["quality"]);

            Assert.IsType<bool>(restored.DataValues["enabled"]);
            Assert.Equal(true, restored.DataValues["enabled"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DataAcquisition.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
