// 应用程序入口，配置 WebHost 与服务。

using DataAcquisition.Application.Abstractions;
using DataAcquisition.Infrastructure.Clients;
using DataAcquisition.Infrastructure.DataStorages;
using DataAcquisition.Infrastructure.Queues;
using DataAcquisition.Infrastructure.DataAcquisitions;
using DataAcquisition.Gateway.BackgroundServices;
using DataAcquisition.Infrastructure.DeviceConfigs;
using DataAcquisition.Infrastructure.Metrics;
using Prometheus;
using Serilog;
using Serilog.Events;
using DataAcquisition.Infrastructure.Logs;

var builder = WebApplication.CreateBuilder(args);
// 从配置读取 URL，支持环境变量和配置文件
var urls = builder.Configuration["Urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000";
builder.WebHost.UseUrls(urls);
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
builder.Services.AddSingleton<DataAcquisition.Gateway.Services.MetricsBridge>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();
builder.Services.AddSingleton<IPLCClientFactory, PLCClientFactory>();
builder.Services.AddSingleton<IPLCClientLifecycleService, PLCClientLifecycleService>();
builder.Services.AddSingleton<IAcquisitionStateManager, AcquisitionStateManager>();
builder.Services.AddSingleton<IHeartbeatMonitor, HeartbeatMonitor>();
builder.Services.AddSingleton<IChannelCollector, ChannelCollector>();
// 存储：Parquet 作为 WAL，后台重传到 Influx
builder.Services.AddSingleton<ParquetFileStorageService>();
builder.Services.AddSingleton<InfluxDbDataStorageService>();
builder.Services.AddSingleton<IQueueService, LocalQueueService>();
builder.Services.AddSingleton<IDataAcquisitionService, DataAcquisitionService>();
// 日志查看服务
builder.Services.AddSingleton<ILogViewService, LogViewService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
builder.Services.AddHostedService<QueueHostedService>();
builder.Services.AddHostedService<ParquetRetryWorker>();
builder.Services.AddControllersWithViews();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
builder.Host.UseSerilog();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

// 添加 Prometheus HTTP 指标收集
app.UseHttpMetrics();

// 初始化 System.Diagnostics.Metrics 到 Prometheus 的桥接
app.Services.GetRequiredService<DataAcquisition.Gateway.Services.MetricsBridge>();

app.UseAuthorization();

// 暴露 Prometheus 指标端点
app.MapMetrics("/metrics");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


Log.Logger.Information("Application starting...");

app.Run();

Log.Logger.Information("Application started.");
