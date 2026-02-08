using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Serilog.Core;
using Serilog.Events;

namespace DataAcquisition.Infrastructure.Logs;

/// <summary>
///     使用 Microsoft.Data.Sqlite 的自定义 Serilog Sink
/// </summary>
public class MicrosoftSqliteSink : ILogEventSink, IDisposable
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Queue<LogEvent> _eventQueue = new();
    private readonly Timer _flushTimer;
    private readonly int _batchSize;
    private SqliteConnection? _connection;

    public MicrosoftSqliteSink(string dbPath, int batchSize = 1000, TimeSpan? flushInterval = null)
    {
        _dbPath = dbPath;
        _batchSize = batchSize;

        // 确保目录存在
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 初始化数据库
        InitializeDatabase();

        // 启动定时刷新
        var interval = flushInterval ?? TimeSpan.FromSeconds(5);
        _flushTimer = new Timer(FlushQueue, null, interval, interval);
    }

    private void InitializeDatabase()
    {
        var connectionString = $"Data Source={_dbPath};Cache=Shared;Mode=ReadWriteCreate";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        var sql = @"
            CREATE TABLE IF NOT EXISTS Logs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TimeStamp TEXT NOT NULL,
                Level TEXT NOT NULL,
                Message TEXT NOT NULL,
                MessageTemplate TEXT,
                Exception TEXT,
                Properties TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON Logs(TimeStamp DESC);
            CREATE INDEX IF NOT EXISTS idx_logs_level ON Logs(Level);
        ";

        using var command = new SqliteCommand(sql, _connection);
        command.ExecuteNonQuery();
    }

    public void Emit(LogEvent logEvent)
    {
        if (_connection == null) return;

        lock (_eventQueue)
        {
            _eventQueue.Enqueue(logEvent);

            // 关键日志（Error/Fatal）立即刷新，避免丢失重要信息
            var isCritical = logEvent.Level >= LogEventLevel.Error;

            // 如果队列达到批次大小，或遇到关键日志，立即刷新
            if (_eventQueue.Count >= _batchSize || isCritical)
            {
                _ = Task.Run(() =>
                {
                    try { FlushQueue(); }
                    catch { /* Sink 不应抛异常影响日志调用方 */ }
                });
            }
        }
    }

    private void FlushQueue(object? state = null)
    {
        FlushQueueInternal();
    }

    private void FlushQueueInternal()
    {
        if (_connection == null) return;

        _writeLock.Wait();
        try
        {
            var eventsToWrite = new List<LogEvent>();

            lock (_eventQueue)
            {
                while (_eventQueue.Count > 0 && eventsToWrite.Count < _batchSize)
                {
                    eventsToWrite.Add(_eventQueue.Dequeue());
                }
            }

            if (eventsToWrite.Count == 0) return;

            // 批量写入
            using var transaction = _connection.BeginTransaction();
            try
            {
                var sql = @"
                    INSERT INTO Logs (TimeStamp, Level, Message, MessageTemplate, Exception, Properties)
                    VALUES (@timestamp, @level, @message, @messageTemplate, @exception, @properties)
                ";

                foreach (var logEvent in eventsToWrite)
                {
                    using var command = new SqliteCommand(sql, _connection, transaction);

                    command.Parameters.AddWithValue("@timestamp", logEvent.Timestamp.ToString("O"));
                    command.Parameters.AddWithValue("@level", logEvent.Level.ToString());
                    command.Parameters.AddWithValue("@message", logEvent.RenderMessage());
                    command.Parameters.AddWithValue("@messageTemplate", logEvent.MessageTemplate.Text);
                    command.Parameters.AddWithValue("@exception",
                        logEvent.Exception?.ToString() ?? (object)DBNull.Value);

                    // 序列化 Properties 为 JSON
                    var properties = new Dictionary<string, object>();
                    foreach (var property in logEvent.Properties)
                    {
                        properties[property.Key] = property.Value.ToString();
                    }
                    var propertiesJson = properties.Count > 0
                        ? JsonSerializer.Serialize(properties)
                        : (object)DBNull.Value;
                    command.Parameters.AddWithValue("@properties", propertiesJson);

                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();

        // 刷新剩余的事件
        FlushQueueInternal();

        _connection?.Close();
        _connection?.Dispose();
        _writeLock?.Dispose();
    }
}

