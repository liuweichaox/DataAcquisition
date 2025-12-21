using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;

namespace DataAcquisition.Infrastructure.Logs;

/// <summary>
///     日志查看服务实现
/// </summary>
public class LogViewService : ILogViewService
{
    private static readonly HashSet<string> KnownLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"
    };

    /// <summary>
    ///     日志行正则表达式
    ///     格式: yyyy-MM-dd HH:mm:ss.fff [Level] [SourceContext] Message
    /// </summary>
    private static readonly Regex LogLineRegex = new(
        @"^(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3})\s+\[(\w+)\]\s+(?:\[([^\]]+)\])?\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly string _logsDirectory;

    public LogViewService()
    {
        // 日志文件路径：Logs/log-.txt（相对于应用程序工作目录）
        // 尝试多个可能的路径，确保能找到日志文件
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Logs"),
            Path.Combine(AppContext.BaseDirectory, "Logs"),
            Path.Combine(AppContext.BaseDirectory, "..", "Logs"),
            "Logs" // 直接使用相对路径
        };

        _logsDirectory = possiblePaths.FirstOrDefault(Directory.Exists) ?? possiblePaths[0];
    }

    /// <summary>
    ///     获取日志条目列表
    /// </summary>
    public async Task<(List<LogEntry> Entries, int TotalCount)> GetLogsAsync(
        string? level = null,
        string? keyword = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var allEntries = new List<LogEntry>();

        // 获取所有日志文件（按日期排序，最新的在前）
        var logFiles = GetLogFiles();

        // 从最新的文件开始读取
        foreach (var logFile in logFiles)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var entries = await ParseLogFileAsync(logFile, cancellationToken);
            allEntries.AddRange(entries);
        }

        // 按时间戳降序排序（最新的在前）
        allEntries = allEntries.OrderByDescending(e => e.Timestamp).ToList();

        // 应用过滤
        var filteredEntries = ApplyFilters(allEntries, level, keyword);

        // 获取总数
        var totalCount = filteredEntries.Count;

        // 应用分页
        var pagedEntries = filteredEntries
            .Skip(skip)
            .Take(take)
            .ToList();

        return (pagedEntries, totalCount);
    }

    /// <summary>
    ///     获取可用的日志级别列表
    /// </summary>
    public List<string> GetAvailableLevels()
    {
        var levels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var logFiles = GetLogFiles();

        // 从日志文件中提取所有出现的级别
        foreach (var logFile in logFiles.Take(10)) // 只检查最近10个文件以提高性能
        {
            if (!File.Exists(logFile))
                continue;

            try
            {
                var lines = File.ReadLines(logFile, Encoding.UTF8);
                foreach (var line in lines)
                {
                    var match = LogLineRegex.Match(line);
                    if (match is { Success: true, Groups.Count: >= 3 })
                    {
                        var levelStr = match.Groups[2].Value.Trim();
                        if (!string.IsNullOrEmpty(levelStr)) levels.Add(levelStr);
                    }
                }
            }
            catch
            {
                // 忽略读取错误
            }
        }

        // 如果没有找到任何级别，返回已知的级别列表
        if (levels.Count == 0) return KnownLevels.ToList();

        // 按标准顺序排序
        var orderedLevels = new List<string>();
        var levelOrder = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };

        foreach (var orderedLevel in levelOrder)
            if (levels.Contains(orderedLevel, StringComparer.OrdinalIgnoreCase))
                orderedLevels.Add(orderedLevel);

        // 添加其他未识别的级别
        foreach (var level in levels)
            if (!orderedLevels.Contains(level, StringComparer.OrdinalIgnoreCase))
                orderedLevels.Add(level);

        return orderedLevels;
    }

    /// <summary>
    ///     获取所有日志文件路径（按日期降序）
    /// </summary>
    private List<string> GetLogFiles()
    {
        if (!Directory.Exists(_logsDirectory)) return new List<string>();

        try
        {
            var files = Directory.GetFiles(_logsDirectory, "log-*.txt")
                .OrderByDescending(f => f) // 按文件名降序（文件名包含日期）
                .ToList();

            return files;
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    ///     解析日志文件
    /// </summary>
    private async Task<List<LogEntry>> ParseLogFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var entries = new List<LogEntry>();

        if (!File.Exists(filePath)) return entries;

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8, cancellationToken);
            var currentEntry = (LogEntry?)null;
            var exceptionBuilder = new StringBuilder();

            foreach (var line in lines)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var match = LogLineRegex.Match(line);

                if (match.Success)
                {
                    // 如果之前有未完成的异常条目，先保存它
                    if (currentEntry != null)
                    {
                        if (exceptionBuilder.Length > 0)
                        {
                            currentEntry.Exception = exceptionBuilder.ToString().Trim();
                            exceptionBuilder.Clear();
                        }

                        entries.Add(currentEntry);
                    }

                    // 解析新的日志条目
                    var timestampStr = match.Groups[1].Value;
                    var level = match.Groups[2].Value.Trim();
                    var source = match.Groups.Count > 3 && match.Groups[3].Success
                        ? match.Groups[3].Value.Trim()
                        : string.Empty;
                    var message = match.Groups.Count > 4 && match.Groups[4].Success
                        ? match.Groups[4].Value.Trim()
                        : string.Empty;

                    if (DateTime.TryParseExact(timestampStr, "yyyy-MM-dd HH:mm:ss.fff",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                        currentEntry = new LogEntry
                        {
                            Timestamp = timestamp,
                            Level = level,
                            Source = source,
                            Message = message
                        };
                }
                else if (currentEntry != null)
                {
                    // 多行异常信息或消息续行
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (exceptionBuilder.Length > 0) exceptionBuilder.AppendLine();
                    }
                    else
                    {
                        if (exceptionBuilder.Length > 0)
                            exceptionBuilder.AppendLine(line);
                        else
                            // 可能是消息的续行
                            currentEntry.Message += Environment.NewLine + line;
                    }
                }
            }

            // 处理最后一个条目
            if (currentEntry != null)
            {
                if (exceptionBuilder.Length > 0) currentEntry.Exception = exceptionBuilder.ToString().Trim();
                entries.Add(currentEntry);
            }
        }
        catch (Exception)
        {
            // 忽略解析错误，返回已解析的条目
        }

        return entries;
    }

    /// <summary>
    ///     应用过滤条件
    /// </summary>
    private List<LogEntry> ApplyFilters(List<LogEntry> entries, string? level, string? keyword)
    {
        var filtered = entries.AsEnumerable();

        // 按级别过滤
        if (!string.IsNullOrWhiteSpace(level))
            filtered = filtered.Where(e =>
                string.Equals(e.Level, level, StringComparison.OrdinalIgnoreCase));

        // 按关键词过滤
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordLower = keyword.ToLowerInvariant();
            filtered = filtered.Where(e =>
                (e.Message?.ToLowerInvariant().Contains(keywordLower) ?? false) ||
                (e.Source?.ToLowerInvariant().Contains(keywordLower) ?? false) ||
                (e.Exception?.ToLowerInvariant().Contains(keywordLower) ?? false));
        }

        return filtered.ToList();
    }
}