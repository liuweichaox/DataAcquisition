using System.Diagnostics;
using System.Net.NetworkInformation;
using DataAcquisition.Core.Communication;
using DataAcquisition.Core.DataAcquisitions;
using DataAcquisition.Core.DataProcessing;
using DataAcquisition.Core.DataStorages;
using DataAcquisition.Core.DeviceConfigs;
using DataAcquisition.Core.Messages;
using DataAcquisition.Core.Queues;
using DataAcquisition.Gateway;
using DataAcquisition.Gateway.Hubs;
using DataAcquisition.Gateway.Infrastructure.Communication;
using DataAcquisition.Gateway.Infrastructure.DataProcessing;
using DataAcquisition.Gateway.Infrastructure.DataStorages;
using DataAcquisition.Gateway.Infrastructure.Messages;
using DataAcquisition.Gateway.Infrastructure.Queues;

var port = 5000;
var isPortInUse = IPGlobalProperties.GetIPGlobalProperties()
    .GetActiveTcpListeners()
    .Any(endpoint => endpoint.Port == port);

if (isPortInUse)
{
    Console.WriteLine("程序已在运行（端口占用），禁止重复启动！");
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://*:5000");
// Add services to the container.
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IMessageService, MessageService>();
builder.Services.AddSingleton<ICommunicationFactory, CommunicationFactory>();
builder.Services.AddSingleton<IDataStorageFactory, DataStorageFactory>();
builder.Services.AddSingleton<IQueueFactory, QueueFactory>();
builder.Services.AddSingleton<IDataAcquisitionService, DataAcquisitionService>();
builder.Services.AddSingleton<IDataProcessingService, DataProcessingService>();
builder.Services.AddSingleton<IDeviceConfigService, DeviceConfigService>();

builder.Services.AddHostedService<DataAcquisitionHostedService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    const string url = "http://localhost:5000";  // 你的 API 地址
    _ = Task.Delay(2000).ContinueWith(_ => Process.Start(new ProcessStartInfo
    {
        FileName = url,
        UseShellExecute = true
    }));
}

app.MapHub<DataHub>("/dataHub");

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
