// Central Web（中心侧）：提供中心 API（边缘注册/心跳/数据接入、查询与管理）。

using Serilog;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// 从配置读取 URL，支持环境变量和配置文件
var urls = builder.Configuration["Urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:8000";
builder.WebHost.UseUrls(urls);

builder.Services.AddHttpClient();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<DataAcquisition.Central.Web.Services.EdgeRegistry>();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");

app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// Prometheus 指标（中心自身进程）
app.UseHttpMetrics();
app.MapMetrics();

// Attribute routing（/api/..）
app.MapControllers();

// MVC（如果仍保留 Views）
app.MapControllerRoute(
    "default",
    "{controller=Home}/{action=Index}/{id?}");

app.Run();