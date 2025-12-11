// 应用程序入口，配置 WebHost 与服务。
using System.Text.Json;
using System.Text.Json.Serialization;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Gateway.Hubs;
using DataAcquisition.Infrastructure.Clients;
using DataAcquisition.Infrastructure.DataProcessing;
using DataAcquisition.Infrastructure.DataStorages;
using DataAcquisition.Infrastructure.Queues;
using DataAcquisition.Infrastructure.DataAcquisitions;
using DataAcquisition.Application;
using DataAcquisition.Gateway.BackgroundServices;
using DataAcquisition.Infrastructure.OperationalEvents;
using DataAcquisition.Infrastructure.DeviceConfigs;
using DataAcquisition.Infrastructure.Metrics;
using DataAcquisition.Gateway.Services;
using Prometheus;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8000");
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
builder.Services.AddSingleton<OpsEventChannel>();
builder.Services.AddSingleton<IOpsEventBus>(sp => sp.GetRequiredService<OpsEventChannel>());
builder.Services.AddSingleton<IOperationalEventsService, OperationalEventsService>();
builder.Services.AddSingleton<IPlcClientFactory, PlcClientFactory>();
builder.Services.AddSingleton<IPlcStateManager, PlcStateManager>();
builder.Services.AddSingleton<IAcquisitionStateManager, AcquisitionStateManager>();
builder.Services.AddSingleton<ITriggerEvaluator, TriggerEvaluator>();
builder.Services.AddSingleton<IHeartbeatMonitor, HeartbeatMonitor>();
builder.Services.AddSingleton<IChannelCollector, ChannelCollector>();
// 注册存储服务（Influx 主库 + Parquet 降级）
builder.Services.AddSingleton<InfluxDbDataStorageService>();
builder.Services.AddSingleton<ParquetFileStorageService>();
builder.Services.AddSingleton<IDataStorageService, FallbackDataStorageService>();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();
builder.Services.AddSingleton<IQueueService, LocalQueueService>();
builder.Services.AddSingleton<IDataAcquisitionService, DataAcquisitionService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
builder.Services.AddHostedService<QueueHostedService>();
builder.Services.AddHostedService<OpsEventBroadcastWorker>();
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

// 暴露 Prometheus 指标端点
app.MapMetrics();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
