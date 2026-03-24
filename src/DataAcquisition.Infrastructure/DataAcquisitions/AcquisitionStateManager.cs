using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.DataAcquisitions;

/// <summary>
///     采集周期状态管理器。热路径使用内存字典，活跃周期同时镜像到本地 SQLite，
///     以便进程重启后恢复条件采集的 active cycle。
/// </summary>
public class AcquisitionStateManager : IAcquisitionStateManager
{
    private readonly ConcurrentDictionary<string, AcquisitionCycle> _activeCycles = new();
    private readonly string _connectionString;
    private readonly object _dbLock = new();
    private readonly ILogger<AcquisitionStateManager> _logger;

    public AcquisitionStateManager(IConfiguration configuration, ILogger<AcquisitionStateManager> logger)
    {
        _logger = logger;

        var dbPath = configuration["Acquisition:StateStore:DatabasePath"] ?? "Data/acquisition-state.db";
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        EnsureSchema();
        LoadActiveCycles();
    }

    public AcquisitionCycle StartCycle(string plcCode, string channelCode, string measurement)
    {
        var key = $"{plcCode}:{channelCode}:{measurement}";
        var cycle = new AcquisitionCycle
        {
            CycleId = Guid.NewGuid().ToString(),
            Measurement = measurement,
            PlcCode = plcCode,
            ChannelCode = channelCode
        };
        _activeCycles[key] = cycle;
        SaveCycle(cycle);
        return cycle;
    }

    public AcquisitionCycle? EndCycle(string plcCode, string channelCode, string measurement)
    {
        var key = $"{plcCode}:{channelCode}:{measurement}";
        var removed = _activeCycles.TryRemove(key, out var cycle) ? cycle : null;
        DeleteCycle(plcCode, channelCode, measurement);
        return removed;
    }

    public AcquisitionCycle? GetActiveCycle(string plcCode, string channelCode, string measurement)
    {
        _activeCycles.TryGetValue(GetKey(plcCode, channelCode, measurement), out var cycle);
        return cycle;
    }

    private void EnsureSchema()
    {
        lock (_dbLock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                              CREATE TABLE IF NOT EXISTS active_cycles (
                                cycle_key    TEXT PRIMARY KEY,
                                cycle_id     TEXT NOT NULL,
                                plc_code     TEXT NOT NULL,
                                channel_code TEXT NOT NULL,
                                measurement  TEXT NOT NULL,
                                updated_utc  TEXT NOT NULL
                              );
                              """;
            cmd.ExecuteNonQuery();
        }
    }

    private void LoadActiveCycles()
    {
        lock (_dbLock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                              SELECT cycle_id, plc_code, channel_code, measurement
                              FROM active_cycles;
                              """;

            using var reader = cmd.ExecuteReader();
            var count = 0;
            while (reader.Read())
            {
                var cycle = new AcquisitionCycle
                {
                    CycleId = reader.GetString(0),
                    PlcCode = reader.GetString(1),
                    ChannelCode = reader.GetString(2),
                    Measurement = reader.GetString(3)
                };

                _activeCycles[GetKey(cycle.PlcCode, cycle.ChannelCode, cycle.Measurement)] = cycle;
                count++;
            }

            if (count > 0)
                _logger.LogInformation("已从本地状态库恢复 {Count} 个活跃采集周期", count);
        }
    }

    private void SaveCycle(AcquisitionCycle cycle)
    {
        lock (_dbLock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO active_cycles(cycle_key, cycle_id, plc_code, channel_code, measurement, updated_utc)
                              VALUES ($cycle_key, $cycle_id, $plc_code, $channel_code, $measurement, $updated_utc)
                              ON CONFLICT(cycle_key) DO UPDATE SET
                                cycle_id = excluded.cycle_id,
                                plc_code = excluded.plc_code,
                                channel_code = excluded.channel_code,
                                measurement = excluded.measurement,
                                updated_utc = excluded.updated_utc;
                              """;
            cmd.Parameters.AddWithValue("$cycle_key", GetKey(cycle.PlcCode, cycle.ChannelCode, cycle.Measurement));
            cmd.Parameters.AddWithValue("$cycle_id", cycle.CycleId);
            cmd.Parameters.AddWithValue("$plc_code", cycle.PlcCode);
            cmd.Parameters.AddWithValue("$channel_code", cycle.ChannelCode);
            cmd.Parameters.AddWithValue("$measurement", cycle.Measurement);
            cmd.Parameters.AddWithValue("$updated_utc", DateTimeOffset.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    private void DeleteCycle(string plcCode, string channelCode, string measurement)
    {
        lock (_dbLock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM active_cycles WHERE cycle_key = $cycle_key;";
            cmd.Parameters.AddWithValue("$cycle_key", GetKey(plcCode, channelCode, measurement));
            cmd.ExecuteNonQuery();
        }
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    private static string GetKey(string plcCode, string channelCode, string measurement) =>
        $"{plcCode}:{channelCode}:{measurement}";
}
