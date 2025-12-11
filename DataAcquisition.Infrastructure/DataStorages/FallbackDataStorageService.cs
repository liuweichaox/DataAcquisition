using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using DataAcquisition.Infrastructure.OperationalEvents;

namespace DataAcquisition.Infrastructure.DataStorages;

/// <summary>
/// 降级存储服务：优先 InfluxDB，失败时降级到 Parquet 文件
/// </summary>
public class FallbackDataStorageService : IDataStorageService
{
    private readonly IDataStorageService _primaryStorage; // InfluxDB
    private readonly ParquetFileStorageService _fallbackStorage; // Parquet
    private readonly IOperationalEventsService _events;

    public FallbackDataStorageService(
        InfluxDbDataStorageService primaryStorage,
        ParquetFileStorageService fallbackStorage,
        IOperationalEventsService events)
    {
        _primaryStorage = primaryStorage;
        _fallbackStorage = fallbackStorage;
        _events = events;
    }

    /// <summary>
    /// 保存单条数据消息
    /// </summary>
    public async Task SaveAsync(DataMessage dataMessage)
    {
        try
        {
            // 优先尝试写入 InfluxDB
            await _primaryStorage.SaveAsync(dataMessage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // InfluxDB 写入失败，降级到 DuckDB
            await _events.WarnAsync($"InfluxDB 写入失败，降级到 DuckDB: {ex.Message}").ConfigureAwait(false);
            try
            {
                await _fallbackStorage.SaveAsync(dataMessage).ConfigureAwait(false);
                await _events.InfoAsync($"数据已降级存储到 DuckDB: {dataMessage.Measurement}").ConfigureAwait(false);
            }
            catch (Exception fallbackEx)
            {
                await _events.ErrorAsync($"DuckDB 降级存储也失败: {fallbackEx.Message}", fallbackEx).ConfigureAwait(false);
                throw; // 如果降级存储也失败，抛出异常
            }
        }
    }

    /// <summary>
    /// 批量保存数据消息
    /// </summary>
    public async Task SaveBatchAsync(List<DataMessage> dataPoints)
    {
        try
        {
            // 优先尝试批量写入 InfluxDB
            await _primaryStorage.SaveBatchAsync(dataPoints).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // InfluxDB 批量写入失败，降级到 DuckDB
            await _events.WarnAsync($"InfluxDB 批量写入失败，降级到 DuckDB: {ex.Message}").ConfigureAwait(false);
            try
            {
                await _fallbackStorage.SaveBatchAsync(dataPoints).ConfigureAwait(false);
                await _events.InfoAsync($"批量数据已降级存储到 DuckDB: {dataPoints.Count} 条").ConfigureAwait(false);
            }
            catch (Exception fallbackEx)
            {
                await _events.ErrorAsync($"DuckDB 降级存储也失败: {fallbackEx.Message}", fallbackEx).ConfigureAwait(false);
                throw; // 如果降级存储也失败，抛出异常
            }
        }
    }

    /// <summary>
    /// 更新记录
    /// </summary>
    public async Task UpdateAsync(string measurement, Dictionary<string, object> values, Dictionary<string, object> conditions)
    {
        try
        {
            await _primaryStorage.UpdateAsync(measurement, values, conditions).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _events.WarnAsync($"InfluxDB 更新失败，降级到 DuckDB: {ex.Message}").ConfigureAwait(false);
            await _fallbackStorage.UpdateAsync(measurement, values, conditions).ConfigureAwait(false);
        }
    }
}
