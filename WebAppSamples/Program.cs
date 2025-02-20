using DataAcquisition.Services.DataAcquisitions;
using Microsoft.AspNetCore.SignalR;
using WebAppSamples.Hubs;
using WebAppSamples.Services.DataAcquisitionConfigs;
using WebAppSamples.Services.DataStorages;
using WebAppSamples.Services.Messages;
using WebAppSamples.Services.PlcClients;
using WebAppSamples.Services.QueueManagers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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