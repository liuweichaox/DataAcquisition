// 应用程序入口，配置 WebHost 与服务。
using System.Text.Json;
using System.Text.Json.Serialization;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Gateway.Hubs;
using DataAcquisition.Infrastructure.Clients;
using DataAcquisition.Infrastructure.DataStorages;
using DataAcquisition.Infrastructure.Queues;
using DataAcquisition.Infrastructure.DataAcquisitions;
using DataAcquisition.Application;
using DataAcquisition.Gateway.BackgroundServices;
using DataAcquisition.Infrastructure.OperationalEvents;
using DataAcquisition.Infrastructure.DeviceConfigs;
using DataAcquisition.Infrastructure.Metrics;
using DataAcquisition.Infrastructure;
using DataAcquisition.Gateway.Services;
using Prometheus;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
// 从配置读取 URL，支持环境变量和配置文件
var urls = builder.Configuration["Urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:8000";
builder.WebHost.UseUrls(urls);
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSignalR().AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
builder.Services.AddSingleton<DataAcquisition.Gateway.Services.MetricsBridge>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();
// 运行事件系统：使用观察者模式，职责分离
builder.Services.AddSingleton<OpsEventChannel>();
builder.Services.AddSingleton<IOpsEventBus>(sp => sp.GetRequiredService<OpsEventChannel>());
builder.Services.AddSingleton<IOperationalEventsService, OperationalEventsService>();

// 注册事件订阅者（可以灵活添加/移除）
builder.Services.AddSingleton<IOpsEventSubscriber, LoggingEventSubscriber>();
builder.Services.AddSingleton<IOpsEventSubscriber, SignalREventSubscriber>();

// 事件分发器：从通道读取事件并分发给所有订阅者
builder.Services.AddHostedService<OpsEventDispatcher>();
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

builder.Services.AddHostedService<DataAcquisitionHostedService>();
builder.Services.AddHostedService<QueueHostedService>();
// OpsEventBroadcastWorker 已被 OpsEventDispatcher 替代
builder.Services.AddHostedService<ParquetRetryWorker>();
builder.Services.AddControllersWithViews();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.MapHub<DataHub>("/dataHub");

app.UseStaticFiles();

app.UseRouting();

// 添加 Prometheus HTTP 指标收集
app.UseHttpMetrics();

// 初始化 System.Diagnostics.Metrics 到 Prometheus 的桥接
var metricsBridge = app.Services.GetRequiredService<DataAcquisition.Gateway.Services.MetricsBridge>();

app.UseAuthorization();

// 暴露 Prometheus 指标端点（避开 /metrics 页面冲突）
app.MapMetrics("/metrics/raw");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
