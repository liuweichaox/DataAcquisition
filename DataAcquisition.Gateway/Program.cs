using System.Text.Json;
using System.Text.Json.Serialization;
using DataAcquisition.Core.Clients;
using DataAcquisition.Core.DataAcquisitions;
using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.DeviceConfigs;
using DataAcquisition.Core.OperationalEvents;
using DataAcquisition.Gateway;
using DataAcquisition.Gateway.Hubs;
using DataAcquisition.Gateway.Infrastructure.Clients;
using DataAcquisition.Gateway.Infrastructure.DataProcessing;
using DataAcquisition.Gateway.Infrastructure.DataStorages;
using DataAcquisition.Gateway.Infrastructure.OperationalEvents;
using DataAcquisition.Gateway.Infrastructure.Queues;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8000");
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();
builder.Services.AddSingleton<IOperationalEvents, OperationalEvents>();
builder.Services.AddSingleton<IPlcClientFactory, PlcClientFactory>();
builder.Services.AddSingleton<IQueue, LocalQueue>();
builder.Services.AddSingleton<IDataStorage, MySqlDataStorage>();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();
builder.Services.AddSingleton<IDataAcquisitionService, DataAcquisitionService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();
builder.Services.AddHostedService<QueueHostedService>();
builder.Services.AddControllersWithViews();

builder.Services.AddSignalR().AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",                 // 按天滚动：log-20250910.txt
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,            // 保留 30 天
        shared: true)                          // IIS/多进程时建议开
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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
