using Microsoft.Data.Sqlite;

namespace DataAcquisition.Central.Web.Services;

public sealed class EdgeRegistry
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public EdgeRegistry(IConfiguration configuration)
    {
        _dbPath = configuration["Central:DatabasePath"] ?? "Data/central.db";
        if (!Path.IsPathRooted(_dbPath)) _dbPath = Path.Combine(AppContext.BaseDirectory, _dbPath);

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        EnsureSchema();
    }

    public IReadOnlyCollection<EdgeState> List()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          SELECT edge_id, workshop_id, hostname, version, last_seen_utc, buffer_backlog, last_error
                          FROM edges
                          ORDER BY last_seen_utc DESC;
                          """;

        using var reader = cmd.ExecuteReader();
        var list = new List<EdgeState>();
        while (reader.Read())
        {
            list.Add(new EdgeState(reader.GetString(0), reader.GetString(1))
            {
                Hostname = reader.IsDBNull(2) ? null : reader.GetString(2),
                Version = reader.IsDBNull(3) ? null : reader.GetString(3),
                LastSeenUtc = ParseUtc(reader.IsDBNull(4) ? null : reader.GetString(4)),
                BufferBacklog = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                LastError = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return list;
    }

    public EdgeState Upsert(string workshopId, string edgeId, string? hostname, string? version, DateTimeOffset nowUtc)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO edges(edge_id, workshop_id, hostname, version, last_seen_utc, buffer_backlog, last_error)
                          VALUES ($edge_id, $workshop_id, $hostname, $version, $last_seen_utc, NULL, NULL)
                          ON CONFLICT(edge_id) DO UPDATE SET
                            workshop_id   = excluded.workshop_id,
                            hostname      = COALESCE(excluded.hostname, edges.hostname),
                            version       = COALESCE(excluded.version, edges.version),
                            last_seen_utc = excluded.last_seen_utc;
                          """;
        cmd.Parameters.AddWithValue("$edge_id", edgeId);
        cmd.Parameters.AddWithValue("$workshop_id", workshopId);
        cmd.Parameters.AddWithValue("$hostname", (object?)hostname ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$version", (object?)version ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$last_seen_utc", nowUtc.ToUniversalTime().ToString("O"));
        cmd.ExecuteNonQuery();

        return Get(edgeId) ?? new EdgeState(edgeId, workshopId)
        {
            Hostname = hostname,
            Version = version,
            LastSeenUtc = nowUtc
        };
    }

    public EdgeState Heartbeat(string workshopId, string edgeId, long? backlog, string? lastError, DateTimeOffset nowUtc)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO edges(edge_id, workshop_id, hostname, version, last_seen_utc, buffer_backlog, last_error)
                          VALUES ($edge_id, $workshop_id, NULL, NULL, $last_seen_utc, $buffer_backlog, $last_error)
                          ON CONFLICT(edge_id) DO UPDATE SET
                            workshop_id    = excluded.workshop_id,
                            last_seen_utc  = excluded.last_seen_utc,
                            buffer_backlog = excluded.buffer_backlog,
                            last_error     = excluded.last_error;
                          """;
        cmd.Parameters.AddWithValue("$edge_id", edgeId);
        cmd.Parameters.AddWithValue("$workshop_id", workshopId);
        cmd.Parameters.AddWithValue("$last_seen_utc", nowUtc.ToUniversalTime().ToString("O"));
        cmd.Parameters.AddWithValue("$buffer_backlog", (object?)backlog ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$last_error", (object?)lastError ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        return Get(edgeId) ?? new EdgeState(edgeId, workshopId)
        {
            LastSeenUtc = nowUtc,
            BufferBacklog = backlog,
            LastError = lastError
        };
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS edges (
                            edge_id        TEXT PRIMARY KEY,
                            workshop_id    TEXT NOT NULL,
                            hostname       TEXT NULL,
                            version        TEXT NULL,
                            last_seen_utc  TEXT NOT NULL,
                            buffer_backlog INTEGER NULL,
                            last_error     TEXT NULL
                          );
                          CREATE INDEX IF NOT EXISTS ix_edges_workshop_id ON edges(workshop_id);
                          """;
        cmd.ExecuteNonQuery();
    }

    private EdgeState? Get(string edgeId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          SELECT edge_id, workshop_id, hostname, version, last_seen_utc, buffer_backlog, last_error
                          FROM edges
                          WHERE edge_id = $edge_id;
                          """;
        cmd.Parameters.AddWithValue("$edge_id", edgeId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new EdgeState(reader.GetString(0), reader.GetString(1))
        {
            Hostname = reader.IsDBNull(2) ? null : reader.GetString(2),
            Version = reader.IsDBNull(3) ? null : reader.GetString(3),
            LastSeenUtc = ParseUtc(reader.IsDBNull(4) ? null : reader.GetString(4)),
            BufferBacklog = reader.IsDBNull(5) ? null : reader.GetInt64(5),
            LastError = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }

    private static DateTimeOffset ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DateTimeOffset.UtcNow;
        return DateTimeOffset.Parse(value).ToUniversalTime();
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

