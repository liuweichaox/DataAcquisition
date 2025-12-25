// Central API（中心侧）：提供中心 API（边缘注册/心跳/数据接入、查询与管理）。

using System.Text.Json;
using DataAcquisition.Central.Api.HealthChecks;
using Serilog;
using Prometheus;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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
app.MapMetrics();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    error = e.Value.Exception?.Message
                })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

// Attribute routing（/api/..）
app.MapControllers();

// 方便验证服务是否启动（不提供页面）
app.MapGet("/", () => Results.Ok(new
{
    service = "DataAcquisition.Central.Api",
    endpoints = new
    {
        edges = "/api/edges",
        telemetry = "/api/telemetry/ingest",
        metrics = "/metrics",
        metricsJson = "/api/metrics-data"
    }
}));

app.Run();