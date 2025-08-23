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

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IMessage, Message>();
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
}

app.MapHub<DataHub>("/dataHub");

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
