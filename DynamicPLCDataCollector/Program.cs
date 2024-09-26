﻿using DynamicPLCDataCollector.DataStorages;
using DynamicPLCDataCollector.Models;
using DynamicPLCDataCollector.PLCClients;
using DynamicPLCDataCollector.Utils;

Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集程序已启动...");

var baseDirectory = AppContext.BaseDirectory;

var deviceConfigPath = Path.Combine(baseDirectory, "Configs", "devices.json");
var metricTableConfigPath = Path.Combine(baseDirectory, "Configs", "MetricConfigs");

var devices = await JsonUtils.LoadConfigAsync<List<Device>>(deviceConfigPath);
var metricTableConfigs = await JsonUtils.LoadAllJsonFilesAsync<MetricTableConfig>(metricTableConfigPath);


IPLCClientManager clientManager = new PLCClientManager(devices);

IDataStorage dataStorage = new SQLiteDataStorage();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += async (sender, e) =>
{
    e.Cancel = true;
    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 捕获到控制台关闭信号 (Ctrl+C 或窗口关闭)...");
    await HandleExitAsync();
};

AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
{
    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 捕获到程序退出信号...");
    await HandleExitAsync();
};

foreach (var device in devices)
{
    foreach (var metricTableConfig in metricTableConfigs)
    {
        StartCollectionTask(device, metricTableConfig);
    }
}

while (true)
{
    await Task.Delay(1000);
}

void StartCollectionTask(Device device, MetricTableConfig metricTableConfig)
{
    Task.Factory.StartNew(async () =>
    {
        while (true)
        {
            try
            {
                if (metricTableConfig.IsEnabled)
                {
                    var data = await clientManager.ReadAsync(device, metricTableConfig);
                    dataStorage.Save(data, metricTableConfig);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - 采集数据异常: {ex.Message}");
            }
            await Task.Delay(metricTableConfig.CollectionFrequency, cts.Token);
        }
    }, TaskCreationOptions.LongRunning);
}

async Task HandleExitAsync()
{
    cts.Cancel();

    await Task.Run(async () =>
    {
        await clientManager.DisconnectAllAsync();
        dataStorage.ReleaseAll();
    });
}