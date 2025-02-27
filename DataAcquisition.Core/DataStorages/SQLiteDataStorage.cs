using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DataAcquisition.Core.Extensions;
using Microsoft.Data.Sqlite;

namespace DataAcquisition.Core.DataStorages;

/// <summary>
/// SQLite 数据存储实现
/// </summary>
public class SqLiteDataStorage : AbstractDataStorage
{
    private readonly SqliteConnection _connection;
    private readonly DataAcquisitionConfig _config;

    public SqLiteDataStorage(DataAcquisitionConfig config) : base(config)
    {
        _config = config;
        _connection = new SqliteConnection(config.ConnectionString);
        _connection.Open();
    }

    public override async Task SaveAsync(DataPoint dataPoint)
    {
        await _connection.InsertAsync(dataPoint);
    }

    public override async Task SaveBatchAsync(List<DataPoint> dataPoints)
    {
        await _connection.InsertBatchAsync(dataPoints);
    }

    public override void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}