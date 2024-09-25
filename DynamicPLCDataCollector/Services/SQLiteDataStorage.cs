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
            const int batchSize = 1000;
            
            await using var sqLiteConnection = new SQLiteConnection($@"Data Source=db.sqlite;Version=3;");
            
            sqLiteConnection.Open();
            
            var dataBatch = new List<Dictionary<string, object>>();
            
            foreach (var data in queue.GetConsumingEnumerable())
            {
                dataBatch.Add(data);
                
                if (dataBatch.Count >= batchSize)
                {
                    await sqLiteConnection.InsertBatchAsync(tableName, dataBatch);
                    dataBatch.Clear();
                }
            }
            
            if (dataBatch.Count > 0)
            {
                await sqLiteConnection.InsertBatchAsync(tableName, dataBatch);
            }
        }
        
        public void DisconnectAll()
        {
            foreach (var queue in QueueDictionary.Values)
            {
                queue.CompleteAdding();
            }
        }
    }
}