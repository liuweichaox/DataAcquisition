using System.Collections.Concurrent;

namespace DataAcquisition.Central.Web.Services;

public sealed class EdgeRegistry
{
    private readonly ConcurrentDictionary<string, EdgeState> _edges = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<EdgeState> List() => _edges.Values.ToArray();

    public EdgeState Upsert(string workshopId, string edgeId, string? hostname, string? version, DateTimeOffset nowUtc)
    {
        return _edges.AddOrUpdate(
            edgeId,
            _ => new EdgeState(edgeId, workshopId)
            {
                Hostname = hostname,
                Version = version,
                LastSeenUtc = nowUtc
            },
            (_, existing) =>
            {
                existing.WorkshopId = workshopId;
                existing.Hostname = hostname ?? existing.Hostname;
                existing.Version = version ?? existing.Version;
                existing.LastSeenUtc = nowUtc;
                return existing;
            });
    }

    public EdgeState Heartbeat(string workshopId, string edgeId, long? backlog, string? lastError, DateTimeOffset nowUtc)
    {
        var state = _edges.GetOrAdd(edgeId, _ => new EdgeState(edgeId, workshopId));
        state.WorkshopId = workshopId;
        state.LastSeenUtc = nowUtc;
        state.BufferBacklog = backlog;
        state.LastError = lastError;
        return state;
    }

    public sealed class EdgeState
    {
        public EdgeState(string edgeId, string workshopId)
        {
            EdgeId = edgeId;
            WorkshopId = workshopId;
        }

        public string EdgeId { get; }
        public string WorkshopId { get; set; }
        public string? Hostname { get; set; }
        public string? Version { get; set; }
        public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
        public long? BufferBacklog { get; set; }
        public string? LastError { get; set; }
    }
}

