using System;
using System.Collections.Generic;
using System.IO;
using DataAcquisition.Infrastructure.DataAcquisitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DataAcquisition.Core.Tests.Infrastructure;

public sealed class AcquisitionStateManagerTests
{
    [Fact]
    public void StartCycle_ShouldPersistAndRecoverActiveCycle()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"acq-state-{Guid.NewGuid():N}.db");
        try
        {
            var configuration = BuildConfiguration(dbPath);
            var manager1 = new AcquisitionStateManager(configuration, NullLogger<AcquisitionStateManager>.Instance);

            var cycle = manager1.StartCycle("PLC01", "CH01", "production");

            var manager2 = new AcquisitionStateManager(configuration, NullLogger<AcquisitionStateManager>.Instance);
            var restored = manager2.GetActiveCycle("PLC01", "CH01", "production");

            Assert.NotNull(restored);
            Assert.Equal(cycle.CycleId, restored!.CycleId);

            var ended = manager2.EndCycle("PLC01", "CH01", "production");
            Assert.NotNull(ended);

            var manager3 = new AcquisitionStateManager(configuration, NullLogger<AcquisitionStateManager>.Instance);
            Assert.Null(manager3.GetActiveCycle("PLC01", "CH01", "production"));
        }
        finally
        {
            TryDelete(dbPath);
            TryDelete($"{dbPath}-wal");
            TryDelete($"{dbPath}-shm");
        }
    }

    private static IConfiguration BuildConfiguration(string dbPath)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Acquisition:StateStore:DatabasePath"] = dbPath
            })
            .Build();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore cleanup failures in temp path
        }
    }
}
