using DataAcquisition.Core.Communication;
using DataAcquisition.Core.DataAcquisitions;
using DataAcquisition.Core.DataStorages;
using Microsoft.AspNetCore.SignalR;
using DataAcquisition.Gateway;
using DataAcquisition.Gateway.Hubs;
using DataAcquisition.Gateway.Services.DataAcquisitionConfigs;
using DataAcquisition.Gateway.Services.Messages;
using DataAcquisition.Gateway.Services.QueueManagers;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://*:5000");
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDataAcquisitionService>(provider =>
{
    var hubContext = provider.GetService<IHubContext<DataHub>>();
    var dataAcquisitionConfigService = new DataAcquisitionConfigService();
    var plcClientFactory = new PlcClientFactory();
    var dataStorageFactory = new DataStorageFactory();
    var queueManagerFactory = new QueueManagerFactory();
    var messageService = new MessageService(hubContext);
    return new DataAcquisitionService(
        dataAcquisitionConfigService,
        plcClientFactory,
        dataStorageFactory,
        queueManagerFactory,
        messageService);
});

builder.Services.AddControllersWithViews();

var app = builder.Build();
ServiceLocator.Configure(app.Services);

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