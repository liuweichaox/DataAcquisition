// Web 门户：仅负责 UI + 管理端 API（例如 metrics-data），其余采集/写入/日志/指标由 Worker 提供。

using System.Net.Http.Headers;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 从配置读取 URL，支持环境变量和配置文件
var urls = builder.Configuration["Urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:8000";
builder.WebHost.UseUrls(urls);

builder.Services.AddHttpClient();
builder.Services.AddControllersWithViews();

// Worker 基础地址（Web 通过反向代理把 /metrics 与大部分 /api 转发给 Worker）
var workerBaseUrl = builder.Configuration["Worker:BaseUrl"] ?? "http://localhost:8001";

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

// 轻量反向代理：转发 /metrics 和除 /api/metrics-data 外的大部分 /api 请求到 Worker
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    var isMetrics = string.Equals(path, "/metrics", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/metrics/", StringComparison.OrdinalIgnoreCase);
    var isApi = path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);
    var isMetricsData = path.StartsWith("/api/metrics-data", StringComparison.OrdinalIgnoreCase);

    if (!isMetrics && !(isApi && !isMetricsData))
    {
        await next();
        return;
    }

    var targetUri = new Uri(workerBaseUrl.TrimEnd('/') + context.Request.Path + context.Request.QueryString);
    using var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

    // body
    if (!HttpMethods.IsGet(context.Request.Method) &&
        !HttpMethods.IsHead(context.Request.Method) &&
        !HttpMethods.IsDelete(context.Request.Method) &&
        !HttpMethods.IsTrace(context.Request.Method))
    {
        requestMessage.Content = new StreamContent(context.Request.Body);
        if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
            requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
    }

    // headers
    foreach (var header in context.Request.Headers)
    {
        if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase)) continue;

        if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    var httpClient = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
    using var responseMessage =
        await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

    context.Response.StatusCode = (int)responseMessage.StatusCode;

    foreach (var header in responseMessage.Headers)
        context.Response.Headers[header.Key] = header.Value.ToArray();
    foreach (var header in responseMessage.Content.Headers)
        context.Response.Headers[header.Key] = header.Value.ToArray();

    // 某些头由 Kestrel 自动处理，避免重复
    context.Response.Headers.Remove("transfer-encoding");

    await responseMessage.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
});

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    "default",
    "{controller=Home}/{action=Index}/{id?}");

app.Run();