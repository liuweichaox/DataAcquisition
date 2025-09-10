using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

public interface IOperationalEventsService
{
    Task InfoAsync(string deviceCode, string message, object? data = null, CancellationToken ct = default);
    Task WarnAsync(string deviceCode, string message, object? data = null, CancellationToken ct = default);
    Task ErrorAsync(string deviceCode, string message, Exception? ex = null, object? data = null, CancellationToken ct = default);
    Task HeartbeatChangedAsync(string deviceCode, bool ok, string? detail = null, CancellationToken ct = default);
}
