using DynamicPLCDataCollector.Extensions;
using DynamicPLCDataCollector.Models;
using Microsoft.Data.Sqlite;

namespace DynamicPLCDataCollector.DataStorages
{
    /// <summary>
    /// SQLite 数据存储实现
    /// </summary>
    public class SQLiteDataStorage : IDataStorage
    {
        private readonly SqliteConnection _connection;
        public SQLiteDataStorage()
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "db.sqlite"); 
            _connection = new SqliteConnection($@"Data Source={dbPath};");
            _connection.Open();
        }

        public async Task SaveBatchAsync(List<Dictionary<string, object>> data, MetricTableConfig metricTableConfig)
        {
            await _connection.InsertBatchAsync(metricTableConfig.TableName, data);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}