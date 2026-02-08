// Edge Agent（车间侧）：负责 Plc 采集、本地缓冲/落盘、上报中心，以及本地诊断 API（不包含 UI）。

using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using DataAcquisition.Infrastructure.Clients;
using DataAcquisition.Infrastructure.DataAcquisitions;
using DataAcquisition.Infrastructure.DataStorages;
using DataAcquisition.Infrastructure.DeviceConfigs;
using DataAcquisition.Infrastructure.Logs;
using DataAcquisition.Infrastructure.Metrics;
using DataAcquisition.Infrastructure.Queues;
using DataAcquisition.Edge.Agent.BackgroundServices;
using DataAcquisition.Edge.Agent.Services;
using MediatR;
using Prometheus;
using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 支持通过配置/环境变量指定监听地址
var urls = builder.Configuration["Urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:8001";
builder.WebHost.UseUrls(urls);

builder.Services.AddHttpClient();

// 配置 AcquisitionOptions
builder.Services.Configure<AcquisitionOptions>(builder.Configuration.GetSection("Acquisition"));

// 配置 Edge 上报（注册/心跳）
builder.Services.Configure<EdgeReportingOptions>(builder.Configuration.GetSection("Edge"));
builder.Services.AddSingleton<EdgeIdentityService>();

// CQRS/MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DataAcquisition.Application.AssemblyMarker).Assembly));

builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
builder.Services.AddSingleton<MetricsBridge>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();
builder.Services.AddSingleton<IPlcClientFactory, PlcClientFactory>();
builder.Services.AddSingleton<IPlcClientLifecycleService, PlcClientLifecycleService>();
builder.Services.AddSingleton<IAcquisitionStateManager, AcquisitionStateManager>();
builder.Services.AddSingleton<IHeartbeatMonitor, HeartbeatMonitor>();
builder.Services.AddSingleton<IChannelCollector, ChannelCollector>();

// 存储：Parquet 作为 WAL，主存储可替换（默认 InfluxDB）
builder.Services.AddSingleton<IWalStorageService, ParquetFileStorageService>();
builder.Services.AddSingleton<IDataStorageService, InfluxDbDataStorageService>();
builder.Services.AddSingleton<IQueueService, QueueService>();
builder.Services.AddSingleton<IDataAcquisitionService, DataAcquisitionService>();

// 日志查看服务（使用 SQLite）
builder.Services.AddSingleton<ILogViewService, SqliteLogViewService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
builder.Services.AddHostedService<QueueHostedService>();
builder.Services.AddHostedService<ParquetRetryWorker>();
builder.Services.AddHostedService<EdgeCentralReporterHostedService>();

builder.Services.AddControllers();

// Health checks（官方风格）：统一用 /health
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("ok"));

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
var metricsBridge = app.Services.GetRequiredService<MetricsBridge>();
metricsBridge.StartListening();

// 暴露 Prometheus 指标端点
app.MapMetrics();

app.MapControllers();
app.MapHealthChecks("/health");

// 方便验证服务是否启动（不提供页面）
app.MapGet("/", () => Results.Ok(new
{
    service = "DataAcquisition.Edge.Agent",
    endpoints = new
    {
        health = "/health",
        metrics = "/metrics",
        logs = "/api/logs",
        logLevels = "/api/logs/levels",
        plcConnections = "/api/DataAcquisition/plc-connections",
        writeRegister = "/api/DataAcquisition/WriteRegister"
    }
}));

// 解析并显示所有监听地址
var addresses = urls.Split(';', ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var baseAddress = addresses.FirstOrDefault()?.Trim() ?? "http://localhost:8001";

Log.Logger.Information("==================================================================");
Log.Logger.Information("              Edge Agent Service Started");
Log.Logger.Information("==================================================================");
Log.Logger.Information("  Service Addresses:");
foreach (var addr in addresses)
{
    Log.Logger.Information("    > {0}", addr.Trim());
}
Log.Logger.Information("==================================================================");
Log.Logger.Information("  Endpoints:");
Log.Logger.Information("    > Health Check:  {0}/health", baseAddress);
Log.Logger.Information("    > Metrics:       {0}/metrics", baseAddress);
Log.Logger.Information("    > Logs:          {0}/api/logs", baseAddress);
Log.Logger.Information("    > Log Levels:    {0}/api/logs/levels", baseAddress);
Log.Logger.Information("==================================================================");

app.Run();
