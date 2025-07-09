using System.Collections.Concurrent;
using System.Text;

namespace DataAcquisition.Gateway.Logging;

public class StandaloneLogger : IDisposable
{
    private readonly string _logRootPath;
    private readonly ConcurrentQueue<string> _logQueue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cancellationToken = new();
    private readonly Task _processingTask;
    private readonly int _bufferSize;

    public StandaloneLogger(string logRootPath = "Logs", int bufferSize = 100)
    {
        _logRootPath = logRootPath;
        _bufferSize = bufferSize;
        Directory.CreateDirectory(_logRootPath);
        _processingTask = ProcessLogsAsync();
    }

    public void Log(string message)
    {
        _logQueue.Enqueue(message);
        _signal.Release();
    }

    private async Task ProcessLogsAsync()
    {
        while (!_cancellationToken.Token.IsCancellationRequested)
        {
            await _signal.WaitAsync(_cancellationToken.Token);

            if (_logQueue.IsEmpty) continue;
            
            var lines = new string[_bufferSize];
            var count = 0;

            await using var writer = new StreamWriter(GetCurrentLogFile(), append: true, Encoding.UTF8, bufferSize: 8192);
            while (count < _bufferSize && _logQueue.TryDequeue(out var line))
            {
                lines[count++] = line;
            }

            await writer.WriteLineAsync(string.Join(Environment.NewLine, lines, 0, count));
            await writer.FlushAsync();
        }
    }

    private string GetCurrentLogFile()
    {
        var now = DateTime.Now;
        var dir = Path.Combine(_logRootPath, now.ToString("yyyy-MM"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{now:yyyy-MM-dd}.log");
    }
    public void Dispose()
    {
        _cancellationToken.Cancel();
        _signal.Release();
        _processingTask.Wait();
    }
}