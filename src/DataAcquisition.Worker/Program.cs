// Worker 宿主：负责 PLC 采集、存储写入、指标与管理 API（不包含 UI）。

using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using DataAcquisition.Infrastructure.Clients;
using DataAcquisition.Infrastructure.DataAcquisitions;
using DataAcquisition.Infrastructure.DataStorages;
using DataAcquisition.Infrastructure.DeviceConfigs;
using DataAcquisition.Infrastructure.Logs;
using DataAcquisition.Infrastructure.Metrics;
using DataAcquisition.Infrastructure.Queues;
using DataAcquisition.Worker.BackgroundServices;
using DataAcquisition.Worker.Services;
using MediatR;
using Prometheus;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// 支持通过配置/环境变量指定监听地址
var urls = builder.Configuration["Urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:8001";
builder.WebHost.UseUrls(urls);

builder.Services.AddMemoryCache();

// 配置 AcquisitionOptions
builder.Services.Configure<AcquisitionOptions>(builder.Configuration.GetSection("Acquisition"));

// CQRS/MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DataAcquisition.Application.AssemblyMarker).Assembly));

builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
builder.Services.AddSingleton<MetricsBridge>();
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

// 日志查看服务（使用 SQLite）
builder.Services.AddSingleton<ILogViewService, SqliteLogViewService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
builder.Services.AddHostedService<QueueHostedService>();
builder.Services.AddHostedService<ParquetRetryWorker>();

builder.Services.AddControllers();

// 配置 SQLite 日志数据库路径（从配置读取，支持相对路径和绝对路径）
var logDbPath = builder.Configuration["Logging:DatabasePath"] ?? "Data/logs.db";
if (!Path.IsPathRooted(logDbPath)) logDbPath = Path.Combine(AppContext.BaseDirectory, logDbPath);
Directory.CreateDirectory(Path.GetDirectoryName(logDbPath)!);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Sink(new MicrosoftSqliteSink(logDbPath, batchSize: 100, flushInterval: TimeSpan.FromSeconds(2)))
    .CreateLogger();
builder.Host.UseSerilog();

var app = builder.Build();

app.UseRouting();

// 添加 Prometheus HTTP 指标收集
app.UseHttpMetrics();

// 初始化 System.Diagnostics.Metrics 到 Prometheus 的桥接
app.Services.GetRequiredService<MetricsBridge>();

// 暴露 Prometheus 指标端点
app.MapMetrics();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

Log.Logger.Information("Worker starting...");
app.Run();

