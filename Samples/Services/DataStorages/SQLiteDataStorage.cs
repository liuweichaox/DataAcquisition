﻿using DataAcquisition.Models;
using DataAcquisition.Services.DataStorages;
using Microsoft.Data.Sqlite;
using Samples.Extensions;

namespace Samples.Services.DataStorages;
 
/// <summary>
/// SQLite 数据存储实现
/// </summary>
public class SQLiteDataStorage : AbstractDataStorage
{
    private readonly SqliteConnection _connection;
    private readonly Device _device;
    private readonly DataAcquisitionConfig _dataAcquisitionConfig;
    public SQLiteDataStorage(Device device, DataAcquisitionConfig dataAcquisitionConfig):base(device, dataAcquisitionConfig)
    {
        _device = device;
        _dataAcquisitionConfig = dataAcquisitionConfig;
            
        var dbPath = Path.Combine(AppContext.BaseDirectory, $"{dataAcquisitionConfig.DatabaseName}.sqlite"); 
        _connection = new SqliteConnection($@"Data Source={dbPath};");
        _connection.Open();
    }

    public override async Task SaveBatchAsync(List<Dictionary<string, object>> data)
    {
        await _connection.InsertBatchAsync(_dataAcquisitionConfig.TableName, data);
    }

    public override async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}