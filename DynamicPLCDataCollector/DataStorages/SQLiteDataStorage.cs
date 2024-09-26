using System.Collections.Concurrent;
using DynamicPLCDataCollector.Extensions;
using DynamicPLCDataCollector.Models;
using Microsoft.Data.Sqlite;

namespace DynamicPLCDataCollector.DataStorages
{
    /// <summary>
    /// SQLite 数据存储实现
    /// </summary>
    public class SQLiteDataStorage : AbstractDataStorage
    {
        public SQLiteDataStorage(MetricTableConfig metricTableConfig) : base(metricTableConfig)
        {
        }
        protected override async void ProcessQueue(BlockingCollection<Dictionary<string, object>> queue, MetricTableConfig metricTableConfig)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "db.sqlite");
            await using var sqLiteConnection = new SqliteConnection($@"Data Source={dbPath};");
            sqLiteConnection.Open();

            var dataBatch = new List<Dictionary<string, object>>();

            foreach (var data in queue.GetConsumingEnumerable())
            {
                dataBatch.Add(data);

                if (dataBatch.Count >= metricTableConfig.BatchSize)
                {
                    await sqLiteConnection.InsertBatchAsync(metricTableConfig.TableName, dataBatch);
                    dataBatch.Clear();
                }
            }

            if (dataBatch.Count > 0)
            {
                await sqLiteConnection.InsertBatchAsync(metricTableConfig.TableName, dataBatch);
            }
        }
    }
}