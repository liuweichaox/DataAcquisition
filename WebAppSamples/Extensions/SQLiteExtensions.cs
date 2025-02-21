using System.Text.RegularExpressions;
using DataAcquisition.Models;
using Microsoft.Data.Sqlite;

namespace WebAppSamples.Extensions;

/// <summary>
/// SqlLite 拓展类
/// </summary>
public static class SqLiteExtensions
{
    /// <summary>
    /// 插入单条
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static async Task<bool> InsertAsync(this SqliteConnection connection, DataPoint data)
    {
        try
        {
            var columns = string.Join(", ", data.Values.Keys.Select(k => $"`{k}`"));

            var paramMapping = data.Values.Keys.ToDictionary(
                key => key,
                key => Regex.Replace(key, @"[^\w]+", "_").Trim('_')
            );

            var parameters = string.Join(", ", paramMapping.Values.Select(k => $"@{k}"));

            var sql = $"INSERT INTO `{data.TableName}` ({columns}) VALUES ({parameters})";

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var kvp in paramMapping)
            {
                command.Parameters.AddWithValue($"@{kvp.Value}", data.Values[kvp.Key] ?? DBNull.Value);
            }

            var count = await command.ExecuteNonQueryAsync();
            return count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Insert failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 批量插入数据到指定的表中
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="dataBatch">要插入的数据，键为列名，值为对应的值</param>
    public static async Task<bool> InsertBatchAsync(this SqliteConnection connection,
        List<DataPoint> dataBatch)
    {
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var data in dataBatch)
            {
                await connection.InsertAsync(data);
            }

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Insert failed: {ex.Message}");
            transaction.Rollback();
            return false;
        }
    }
}