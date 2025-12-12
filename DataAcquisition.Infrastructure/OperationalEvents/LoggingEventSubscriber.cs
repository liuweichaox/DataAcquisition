using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.OperationalEvents;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Infrastructure.OperationalEvents;

/// <summary>
/// 日志记录事件订阅者。
/// 负责将运行事件记录到日志系统，使用缓存优化性能。
/// </summary>
public sealed class LoggingEventSubscriber : IOpsEventSubscriber
{
    private readonly ILogger<LoggingEventSubscriber> _logger;

    // 使用静态字典缓存日志级别映射，避免重复解析
    private static readonly Dictionary<string, LogLevel> LogLevelCache = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Information", LogLevel.Information },
        { "Warning", LogLevel.Warning },
        { "Error", LogLevel.Error },
        { "Critical", LogLevel.Critical },
        { "Debug", LogLevel.Debug },
        { "Trace", LogLevel.Trace }
    };

    public LoggingEventSubscriber(ILogger<LoggingEventSubscriber> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 处理运行事件并记录日志。
    /// 使用结构化日志，支持数据序列化。
    /// </summary>
    public Task HandleAsync(OpsEvent evt, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        var level = GetLogLevel(evt.Level);

        // 使用结构化日志记录，支持数据对象的序列化
        // {@Data} 格式会序列化整个对象，便于日志分析
        if (evt.Data != null)
        {
            _logger.Log(level, "事件: {Message} | 数据: {@Data}", evt.Message, evt.Data);
        }
        else
        {
            _logger.Log(level, "事件: {Message}", evt.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取日志级别，使用缓存优化性能。
    /// </summary>
    private static LogLevel GetLogLevel(string level)
    {
        return LogLevelCache.TryGetValue(level, out var logLevel)
            ? logLevel
            : LogLevel.Information; // 默认级别
    }
}
