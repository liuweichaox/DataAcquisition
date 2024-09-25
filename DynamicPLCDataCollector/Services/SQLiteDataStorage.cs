using System.Collections.Concurrent;
using System.Data.SQLite;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.Extensions;

namespace DynamicPLCDataCollector.Services
{
    public class SQLiteDataStorage : IDataStorage
    {
        private static readonly ConcurrentDictionary<string, BlockingCollection<Dictionary<string, object>>> QueueDictionary = new();

        public void Save(Dictionary<string, object> data, MetricTableConfig metricTableConfig)
        {
            var queue = QueueDictionary.GetOrAdd(metricTableConfig.TableName, tableName =>
            {
                var newQueue = new BlockingCollection<Dictionary<string, object>>();

                ProcessQueue(tableName, newQueue);
                
                return newQueue;
            });

            queue.Add(data);
        }

        private async Task ProcessQueue(string tableName, BlockingCollection<Dictionary<string, object>> queue)
        {
            await using var sqLiteConnection = new SQLiteConnection($@"Data Source=Data/db.sqlite;Version=3;");
            
            sqLiteConnection.Open();
            
            foreach (var data in queue.GetConsumingEnumerable())
            {
               await sqLiteConnection.InsertAsync(tableName, data);
            }
        }
    }
}