using System.Data;
using Microsoft.Data.Sqlite;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

/// <summary>
/// SQLite-backed local persistence for matches, applications, and outbox.
/// Minimal schema v1: no curriculum/job_posting yet (no consumers).
/// Thread-safe: SQLite handles concurrent access via journal/WAL.
/// </summary>
public interface IDeviceStore : IDisposable
{
    void EnsureSchema();
    void InsertMatch(string id, string clientRef, string jobPostingId, int score, string payloadJson);
    void InsertApplication(string id, string clientRef, string jobPostingId, string status, string payloadJson, DateTime? appliedAt);
    void InsertOutboxMessage(string id, string entityType, string entityId, string payloadJson);
    List<OutboxRow> GetUnprocessedOutbox(int limit);
    void MarkOutboxProcessed(string id);
    void MarkOutboxFailed(string id, string error);
    List<MatchRow> QueryMatches(int? minScore = null, DateTime? updatedSince = null, int limit = 100);
    List<ApplicationRow> QueryApplications(string? status = null, int limit = 100);
}

public class OutboxRow
{
    public string Id { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

public class MatchRow
{
    public string Id { get; set; } = "";
    public string ClientRef { get; set; } = "";
    public string JobPostingId { get; set; } = "";
    public int Score { get; set; }
    public string PayloadJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class ApplicationRow
{
    public string Id { get; set; } = "";
    public string ClientRef { get; set; } = "";
    public string JobPostingId { get; set; } = "";
    public string Status { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTime? AppliedAt { get; set; }
}

public class SqliteDeviceStore : IDeviceStore
{
    private readonly SqliteConnection _conn;

    public SqliteDeviceStore(string dbPath = "")
    {
        var path = string.IsNullOrEmpty(dbPath) ? AppPaths.DeviceDbFile : dbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _conn = new SqliteConnection($"Data Source={path};");
        _conn.Open();
    }

    public void EnsureSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS match (
                id              TEXT PRIMARY KEY,
                client_ref      TEXT UNIQUE,
                job_posting_id  TEXT NOT NULL,
                score           INTEGER NOT NULL,
                payload_json    TEXT NOT NULL,
                created_at      TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS application (
                id              TEXT PRIMARY KEY,
                client_ref      TEXT UNIQUE,
                job_posting_id  TEXT NOT NULL,
                status          TEXT NOT NULL,
                payload_json    TEXT NOT NULL,
                applied_at      TEXT
            );

            CREATE TABLE IF NOT EXISTS outbox (
                id              TEXT PRIMARY KEY,
                entity_type     TEXT NOT NULL,
                entity_id       TEXT NOT NULL,
                payload_json    TEXT NOT NULL,
                created_at      TEXT NOT NULL,
                processed_at    TEXT,
                attempts        INTEGER NOT NULL DEFAULT 0,
                last_error      TEXT
            );

            CREATE INDEX IF NOT EXISTS ix_outbox_unprocessed ON outbox(processed_at) WHERE processed_at IS NULL;
            CREATE INDEX IF NOT EXISTS ix_match_score ON match(score);
            CREATE INDEX IF NOT EXISTS ix_application_status ON application(status);
        ";
        cmd.ExecuteNonQuery();
        AppLogger.Info("SqliteDeviceStore: schema ensured");
    }

    public void InsertMatch(string id, string clientRef, string jobPostingId, int score, string payloadJson)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO match (id, client_ref, job_posting_id, score, payload_json, created_at)
            VALUES (@id, @clientRef, @jobPostingId, @score, @payloadJson, @createdAt)
        ";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@clientRef", clientRef);
        cmd.Parameters.AddWithValue("@jobPostingId", jobPostingId);
        cmd.Parameters.AddWithValue("@score", score);
        cmd.Parameters.AddWithValue("@payloadJson", payloadJson);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void InsertApplication(string id, string clientRef, string jobPostingId, string status, string payloadJson, DateTime? appliedAt)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO application (id, client_ref, job_posting_id, status, payload_json, applied_at)
            VALUES (@id, @clientRef, @jobPostingId, @status, @payloadJson, @appliedAt)
        ";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@clientRef", clientRef);
        cmd.Parameters.AddWithValue("@jobPostingId", jobPostingId);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@payloadJson", payloadJson);
        cmd.Parameters.AddWithValue("@appliedAt", appliedAt?.ToString("o") as object ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void InsertOutboxMessage(string id, string entityType, string entityId, string payloadJson)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO outbox (id, entity_type, entity_id, payload_json, created_at, attempts)
            VALUES (@id, @entityType, @entityId, @payloadJson, @createdAt, 0)
        ";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@entityType", entityType);
        cmd.Parameters.AddWithValue("@entityId", entityId);
        cmd.Parameters.AddWithValue("@payloadJson", payloadJson);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public List<OutboxRow> GetUnprocessedOutbox(int limit)
    {
        var rows = new List<OutboxRow>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, entity_type, entity_id, payload_json, created_at, processed_at, attempts, last_error
            FROM outbox
            WHERE processed_at IS NULL
            ORDER BY created_at
            LIMIT @limit
        ";
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new OutboxRow
            {
                Id = reader.GetString(0),
                EntityType = reader.GetString(1),
                EntityId = reader.GetString(2),
                PayloadJson = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                ProcessedAt = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                Attempts = reader.GetInt32(6),
                LastError = reader.IsDBNull(7) ? null : reader.GetString(7),
            });
        }
        return rows;
    }

    public void MarkOutboxProcessed(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE outbox SET processed_at = @now WHERE id = @id";
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void MarkOutboxFailed(string id, string error)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE outbox SET attempts = attempts + 1, last_error = @error WHERE id = @id";
        cmd.Parameters.AddWithValue("@error", error);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public List<MatchRow> QueryMatches(int? minScore = null, DateTime? updatedSince = null, int limit = 100)
    {
        var rows = new List<MatchRow>();
        using var cmd = _conn.CreateCommand();
        var where = new List<string> { "1=1" };
        if (minScore.HasValue) where.Add("score >= @minScore");
        if (updatedSince.HasValue) where.Add("created_at >= @updatedSince");

        cmd.CommandText = $@"
            SELECT id, client_ref, job_posting_id, score, payload_json, created_at
            FROM match
            WHERE {string.Join(" AND ", where)}
            ORDER BY created_at DESC
            LIMIT @limit
        ";
        if (minScore.HasValue) cmd.Parameters.AddWithValue("@minScore", minScore.Value);
        if (updatedSince.HasValue) cmd.Parameters.AddWithValue("@updatedSince", updatedSince.Value.ToString("o"));
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new MatchRow
            {
                Id = reader.GetString(0),
                ClientRef = reader.GetString(1),
                JobPostingId = reader.GetString(2),
                Score = reader.GetInt32(3),
                PayloadJson = reader.GetString(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
            });
        }
        return rows;
    }

    public List<ApplicationRow> QueryApplications(string? status = null, int limit = 100)
    {
        var rows = new List<ApplicationRow>();
        using var cmd = _conn.CreateCommand();
        var where = "1=1";
        if (!string.IsNullOrEmpty(status)) where = "status = @status";

        cmd.CommandText = $@"
            SELECT id, client_ref, job_posting_id, status, payload_json, applied_at
            FROM application
            WHERE {where}
            ORDER BY applied_at DESC NULLS LAST
            LIMIT @limit
        ";
        if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ApplicationRow
            {
                Id = reader.GetString(0),
                ClientRef = reader.GetString(1),
                JobPostingId = reader.GetString(2),
                Status = reader.GetString(3),
                PayloadJson = reader.GetString(4),
                AppliedAt = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
            });
        }
        return rows;
    }

    public void Dispose()
    {
        try
        {
            if (_conn?.State == System.Data.ConnectionState.Open)
                _conn.Close();
        }
        catch { }
        finally
        {
            _conn?.Dispose();
        }
    }
}
