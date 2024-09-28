using DynamicPLCDataCollector.Extensions;
using Microsoft.Data.Sqlite;

namespace DynamicPLCDataCollector.Services.DataStorages;

/// <summary>
/// SQLite 数据存储实现
/// </summary>
public class SQLiteDataStorage : IDataStorage
{
    private readonly SqliteConnection _connection;
    private readonly MetricTableConfig _metricTableConfig;
    public SQLiteDataStorage(MetricTableConfig metricTableConfig)
    {
        _metricTableConfig = metricTableConfig;
            
        var dbPath = Path.Combine(AppContext.BaseDirectory, $"{metricTableConfig.DatabaseName}.sqlite"); 
        _connection = new SqliteConnection($@"Data Source={dbPath};");
        _connection.Open();
    }

    public async Task SaveBatchAsync(List<Dictionary<string, object>> data)
    {
        await _connection.InsertBatchAsync(_metricTableConfig.TableName, data);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}