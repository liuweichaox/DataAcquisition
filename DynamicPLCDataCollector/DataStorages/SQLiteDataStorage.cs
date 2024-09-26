﻿using System.Collections.Concurrent;
using System.Data.SQLite;
using DynamicPLCDataCollector.Extensions;
using DynamicPLCDataCollector.Models;

namespace DynamicPLCDataCollector.DataStorages
{
    public class SQLiteDataStorage : AbstractDataStorage
    {
        protected override async void ProcessQueue(BlockingCollection<Dictionary<string, object>> queue, MetricTableConfig metricTableConfig)
        {
            await using var sqLiteConnection = new SQLiteConnection($@"Data Source=db.sqlite;Version=3;");
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