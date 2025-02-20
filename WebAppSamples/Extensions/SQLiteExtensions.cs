using System.Text.RegularExpressions;
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
    /// <param name="tableName"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static async Task<bool> InsertAsync(this SqliteConnection connection, string tableName, Dictionary<string, object> data)
    {
        try
        {
            if (data == null || data.Count == 0)
                throw new ArgumentException("数据不能为空", nameof(data));
            
            var columns = string.Join(", ", data.Keys.Select(k => $"`{k}`"));
            
            var paramMapping = data.Keys.ToDictionary(
                key => key,
                key => Regex.Replace(key, @"[^\w]+", "_").Trim('_')
            );
            
            var parameters = string.Join(", ", paramMapping.Values.Select(k => $"@{k}"));

            var sql = $"INSERT INTO `{tableName}` ({columns}) VALUES ({parameters})";

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            foreach (var kvp in paramMapping)
            {
                command.Parameters.AddWithValue($"@{kvp.Value}", data[kvp.Key] ?? DBNull.Value);
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
    /// <param name="tableName">目标表名</param>
    /// <param name="dataBatch">要插入的数据，键为列名，值为对应的值</param>
    public static async Task<bool> InsertBatchAsync(this SqliteConnection connection, string tableName,
        List<Dictionary<string, object>> dataBatch)
    {
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var data in dataBatch)
            {
                await connection.InsertAsync(tableName, data);
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