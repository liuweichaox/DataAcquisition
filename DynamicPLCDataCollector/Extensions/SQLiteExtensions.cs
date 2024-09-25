using System.Data.SQLite;

namespace DynamicPLCDataCollector.Extensions;

public static class SQLiteExtensions
{
    /// <summary>
    /// 插入数据到指定的表中
    /// </summary>
    /// <param name="tableName">目标表名</param>
    /// <param name="data">要插入的数据，键为列名，值为对应的值</param>
    public static void Insert(this SQLiteConnection connection, string tableName, Dictionary<string, object> data)
    {
        var columns = string.Join(", ", data.Keys);
        var parameters = string.Join(", ", data.Keys.Select(key => $"@{key}"));

        var commandText = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
        using var command = new SQLiteCommand(commandText, connection);
        foreach (var kvp in data)
        {
            command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
        }

        command.ExecuteNonQuery();
    }
    
    
    public static void InsertBatch(this SQLiteConnection connection, string tableName, List<Dictionary<string, object>> dataBatch)
    {
        using var transaction = connection.BeginTransaction();

        foreach (var data in dataBatch)
        {
            connection.Insert(tableName, data);  
        }

        transaction.Commit();
    }
}