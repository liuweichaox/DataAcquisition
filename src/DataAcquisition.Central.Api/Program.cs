// Central API（中心侧）：提供中心 API（边缘注册/心跳/数据接入、查询与管理）。

using DataAcquisition.Central.Api.HealthChecks;
using Serilog;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// 从配置读取 URL，支持环境变量和配置文件
var urls = builder.Configuration["Urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:8000";
builder.WebHost.UseUrls(urls);

builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddSingleton<DataAcquisition.Central.Api.Services.EdgeRegistry>();

builder.Services
    .AddHealthChecks()
    .AddCheck<SqliteHealthCheck>("sqlite");

// CORS：给纯前端（Vue CLI / 静态站点）调用 API 用
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        // 支持配置：Cors:AllowedOrigins=["http://localhost:3000", "..."]
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0)
        {
            // 默认允许本地 Vue dev server
            policy.WithOrigins("http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

var app = builder.Build();

app.UseRouting();
app.UseAuthorization();

app.UseCors("frontend");

// Prometheus 指标（中心自身进程）
app.UseHttpMetrics();
// Prometheus 原始指标（官方默认端点）
app.MapMetrics("/metrics");

// Health checks（官方风格）：统一用 /health
app.MapHealthChecks("/health");

// Attribute routing（/api/..）
app.MapControllers();

// 方便验证服务是否启动（不提供页面）
app.MapGet("/", () => Results.Ok(new
{
    service = "DataAcquisition.Central.Api",
    endpoints = new
    {
        edges = "/api/edges",
        metrics = "/metrics",
        metricsJson = "/api/metrics-data"
    }
}));

// 解析并显示所有监听地址
var addresses = urls.Split(';', ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var baseAddress = addresses.FirstOrDefault()?.Trim() ?? "http://localhost:8000";

Log.Logger.Information("╔═══════════════════════════════════════════════════════════╗");
Log.Logger.Information("║        Central API 服务已启动                             ║");
Log.Logger.Information("╠═══════════════════════════════════════════════════════════╣");
Log.Logger.Information("║  服务地址:                                                 ║");
foreach (var addr in addresses)
{
    Log.Logger.Information("║    • {0,-55} ║", addr.Trim());
}
Log.Logger.Information("╠═══════════════════════════════════════════════════════════╣");
Log.Logger.Information("║  主要端点:                                                 ║");
Log.Logger.Information("║    • 健康检查:  {0}/health                                ║", baseAddress);
Log.Logger.Information("║    • Edge 列表: {0}/api/edges                             ║", baseAddress);
Log.Logger.Information("║    • 指标数据:  {0}/metrics                               ║", baseAddress);
Log.Logger.Information("║    • 指标 JSON: {0}/api/metrics-data                      ║", baseAddress);
Log.Logger.Information("║    • Edge 指标: {0}/api/edges/{{edgeId}}/metrics/json      ║", baseAddress);
Log.Logger.Information("║    • Edge 日志: {0}/api/edges/{{edgeId}}/logs              ║", baseAddress);
Log.Logger.Information("╚═══════════════════════════════════════════════════════════╝");

app.Run();