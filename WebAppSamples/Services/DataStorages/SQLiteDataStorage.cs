using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;
using Microsoft.Data.Sqlite;
using WebAppSamples.Extensions;

namespace WebAppSamples.Services.DataStorages;

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
        var dbPath = Path.Combine(AppContext.BaseDirectory, $"{config.DatabaseName}.sqlite");
        _connection = new SqliteConnection($@"Data Source={dbPath};");
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

    public override async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}