using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dapper;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.Models;
using MySqlConnector;
using Newtonsoft.Json;

namespace DataAcquisition.Gateway.DataStorages;

public class MySqlDataStorage : DataStorage
{
    private static readonly Regex ParamCleanRegex = new(@"[^\w]+", RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, (string Sql, Dictionary<string, string> Mapping)> SqlCache = new();
    private readonly string _connectionString;

    public MySqlDataStorage(string connectionString) : base(connectionString)
    {
        _connectionString = connectionString;
    }

    public override async Task SaveAsync(DataMessage dataMessage)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var paramMapping = dataMessage.Values.Keys.ToDictionary(
                key => key,
                key => ParamCleanRegex.Replace(key, "_").Trim('_')
            );

            var columns = string.Join(", ", dataMessage.Values.Keys.Select(k => $"`{k}`"));
            var parameters = string.Join(", ", paramMapping.Values.Select(v => $"@{v}"));
            var sql = $"INSERT INTO `{dataMessage.TableName}` ({columns}) VALUES ({parameters})";

            var dapperParams = new DynamicParameters();
            foreach (var kvp in dataMessage.Values)
            {
                dapperParams.Add(paramMapping[kvp.Key], kvp.Value);
            }

            await connection.ExecuteAsync(sql, dapperParams, commandTimeout: 60);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Insert failed: {ex.Message}\nData: {JsonConvert.SerializeObject(dataMessage)}");
        }
    }

    public override async Task SaveBatchAsync(List<DataMessage> dataMessages)
    {
        if (dataMessages == null || dataMessages.Count == 0)
            return;

        await using var connection = new MySqlConnection(_connectionString);
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            foreach (var dataMessage in dataMessages)
            {
                var cacheKey = $"{dataMessage.TableName}:{string.Join(",", dataMessage.Values.Keys.OrderBy(k => k))}";

                var (sql, paramMapping) = SqlCache.GetOrAdd(cacheKey, _ =>
                {
                    var mapping = dataMessage.Values.Keys.ToDictionary(
                        key => key,
                        key => ParamCleanRegex.Replace(key, "_").Trim('_')
                    );
                    var columns = string.Join(", ", dataMessage.Values.Keys.Select(k => $"`{k}`"));
                    var parameters = string.Join(", ", mapping.Values.Select(v => $"@{v}"));
                    return ($"INSERT INTO `{dataMessage.TableName}` ({columns}) VALUES ({parameters})", mapping);
                });

                var dapperParams = new DynamicParameters();
                foreach (var kvp in dataMessage.Values)
                {
                    dapperParams.Add(paramMapping[kvp.Key], kvp.Value);
                }

                await connection.ExecuteAsync(sql, dapperParams, transaction, commandTimeout: 60);
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"[ERROR] Batch insert failed: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
