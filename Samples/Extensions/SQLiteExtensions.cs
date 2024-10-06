using Microsoft.Data.Sqlite;

namespace Samples.Extensions;

public static class SQLiteExtensions
{
    /// <summary>
    /// 插入数据到指定的表中
    /// </summary>
    /// <param name="tableName">目标表名</param>
    /// <param name="data">要插入的数据，键为列名，值为对应的值</param>
    public static async Task<bool> InsertAsync(this SqliteConnection connection, string tableName, Dictionary<string, object> data)
    {
        try
        {
            var columns = string.Join(", ", data.Keys);
            var parameters = string.Join(", ", data.Keys.Select(key => $"@{key}"));

            var commandText = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
            await using var command = new SqliteCommand(commandText, connection);
            foreach (var kvp in data)
            {
                command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
            }

            var count = await command.ExecuteNonQueryAsync();

            return count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inserting data: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 批量插入数据到指定的表中
    /// </summary>
    /// <param name="tableName">目标表名</param>
    /// <param name="dataBatch">要插入的数据，键为列名，值为对应的值</param>
    public static async Task<bool> InsertBatchAsync(this SqliteConnection connection, string tableName, List<Dictionary<string, object>> dataBatch)
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
            Console.WriteLine($"Error inserting data: {ex.Message}");
            transaction.Rollback();
            return false;
        }
    }
}