using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Newtonsoft.Json;

namespace DataAcquisition.Infrastructure.DataStorages;

/// <summary>
/// 使用 MySqlConnector 实现的数据存储服务。
/// </summary>
public class MySqlDataStorageService : IDataStorageService
{
    private static readonly Regex ParamCleanRegex = new(@"[^\w]+", RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, (string Sql, Dictionary<string, string> Mapping)> SqlCache = new();
    private readonly string _connectionString;
    private readonly IOperationalEventsService _events;
    /// <summary>
    /// 构造函数，初始化连接字符串和事件服务。
    /// </summary>
    public MySqlDataStorageService(IConfiguration configuration, IOperationalEventsService events)
    {
        _connectionString = configuration.GetConnectionString("MySQL") ?? throw new ArgumentNullException("MySql connection string is not configured.");
        _events = events;
    }

    /// <summary>
    /// 保存单条数据消息。
    /// </summary>
    /// <param name="dataMessage">待保存的数据消息</param>
    public async Task SaveAsync(DataMessage dataMessage)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var paramMapping = dataMessage.DataValues.Keys.ToDictionary(
                key => key,
                key => ParamCleanRegex.Replace(key, "_").Trim('_')
            );

            var columns = string.Join(", ", dataMessage.DataValues.Keys.Select(k => $"`{k}`"));
            var parameters = string.Join(", ", paramMapping.Values.Select(v => $"@{v}"));
            var sql = $"INSERT INTO `{dataMessage.TableName}` ({columns}) VALUES ({parameters})";

            var dapperParams = new DynamicParameters();
            foreach (var kvp in dataMessage.DataValues)
            {
                dapperParams.Add(paramMapping[kvp.Key], kvp.Value);
            }

            await connection.ExecuteAsync(sql, dapperParams, commandTimeout: 60);
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync("System", $"[ERROR] Insert failed: {ex.Message}\nData: {JsonConvert.SerializeObject(dataMessage)}", ex);
        }
    }

    /// <summary>
    /// 批量保存数据消息。
    /// </summary>
    /// <param name="dataMessages">数据消息集合</param>
    public async Task SaveBatchAsync(List<DataMessage> dataMessages)
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
                var cacheKey = $"{dataMessage.TableName}:{string.Join(",", dataMessage.DataValues.Keys.OrderBy(k => k))}";

                var (sql, paramMapping) = SqlCache.GetOrAdd(cacheKey, _ =>
                {
                    var mapping = dataMessage.DataValues.Keys.ToDictionary(
                        key => key,
                        key => ParamCleanRegex.Replace(key, "_").Trim('_')
                    );
                    var columns = string.Join(", ", dataMessage.DataValues.Keys.Select(k => $"`{k}`"));
                    var parameters = string.Join(", ", mapping.Values.Select(v => $"@{v}"));
                    return ($"INSERT INTO `{dataMessage.TableName}` ({columns}) VALUES ({parameters})", mapping);
                });

                var dapperParams = new DynamicParameters();
                foreach (var kvp in dataMessage.DataValues)
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
            await _events.ErrorAsync("System", $"[ERROR] Batch insert failed: {ex.Message}\n{ex.StackTrace}", ex);
        }
    }

    /// <summary>
    /// 根据条件更新记录。
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="values">更新的字段和值</param>
    /// <param name="conditions">更新条件</param>
    public async Task UpdateAsync(string tableName, Dictionary<string, object> values, Dictionary<string, object> conditions)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var setMapping = values.Keys.ToDictionary(
                key => key,
                key => ParamCleanRegex.Replace(key, "_").Trim('_')
            );
            var whereMapping = conditions.Keys.ToDictionary(
                key => key,
                key => ParamCleanRegex.Replace(key, "_").Trim('_') + "_w"
            );

            var setClause = string.Join(", ", setMapping.Select(kvp => $"`{kvp.Key}`=@{kvp.Value}"));
            var whereClause = string.Join(" AND ", whereMapping.Select(kvp => $"`{kvp.Key}`=@{kvp.Value}"));
            var sql = $"UPDATE `{tableName}` SET {setClause} WHERE {whereClause}";

            var dapperParams = new DynamicParameters();
            foreach (var kvp in values)
            {
                dapperParams.Add(setMapping[kvp.Key], kvp.Value);
            }
            foreach (var kvp in conditions)
            {
                dapperParams.Add(whereMapping[kvp.Key], kvp.Value);
            }

            await connection.ExecuteAsync(sql, dapperParams, commandTimeout: 60);
        }
        catch (Exception ex)
        {
            await _events.ErrorAsync("System", $"[ERROR] Update failed: {ex.Message}\n{ex.StackTrace}", ex);
        }
    }
}
