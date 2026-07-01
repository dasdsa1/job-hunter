using JobHunterApp.Services;

namespace JobHunterApp.Tests;

public class SqliteDeviceStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteDeviceStore _store;

    public SqliteDeviceStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.db");
        _store = new SqliteDeviceStore(_dbPath);
        _store.EnsureSchema();
    }

    public void Dispose()
    {
        _store?.Dispose();
        // SQLite WAL files may persist briefly; skip deletion to avoid file-lock errors
        // temp files are in %TEMP% anyway, so they'll be cleaned up by OS eventually
    }

    [Fact]
    public void EnsureSchema_CreatesTablesSuccessfully()
    {
        // Schema already created in constructor, verify by inserting
        _store.InsertMatch("id1", "ref1", "job1", 7, "{}");
        var matches = _store.QueryMatches();
        Assert.Single(matches);
    }

    [Fact]
    public void InsertMatch_StoresAndRetrievesCorrectly()
    {
        _store.InsertMatch("match-1", "ref-1", "job-123", 8, """{"score":8}""");
        var matches = _store.QueryMatches();

        Assert.Single(matches);
        Assert.Equal("match-1", matches[0].Id);
        Assert.Equal("ref-1", matches[0].ClientRef);
        Assert.Equal(8, matches[0].Score);
    }

    [Fact]
    public void InsertMatch_IsIdempotentOnClientRef()
    {
        _store.InsertMatch("id1", "ref1", "job1", 7, "{}");
        _store.InsertMatch("id1", "ref1", "job1", 7, "{}"); // duplicate clientRef, should be ignored

        var matches = _store.QueryMatches();
        Assert.Single(matches);
    }

    [Fact]
    public void QueryMatches_FiltersByMinScore()
    {
        _store.InsertMatch("m1", "r1", "j1", 5, "{}");
        _store.InsertMatch("m2", "r2", "j2", 8, "{}");
        _store.InsertMatch("m3", "r3", "j3", 9, "{}");

        var highScores = _store.QueryMatches(minScore: 8);
        Assert.Equal(2, highScores.Count);
        Assert.All(highScores, m => Assert.True(m.Score >= 8));
    }

    [Fact]
    public void InsertApplication_StoresAndRetrievesCorrectly()
    {
        var appliedAt = DateTime.UtcNow;
        _store.InsertApplication("app1", "aref1", "job1", "applied", "{}", appliedAt);

        var apps = _store.QueryApplications();
        Assert.Single(apps);
        Assert.Equal("applied", apps[0].Status);
        Assert.NotNull(apps[0].AppliedAt);
    }

    [Fact]
    public void InsertOutbox_AndMarkProcessed()
    {
        _store.InsertOutboxMessage("outbox-1", "matches", "match-1", """{"id":"match-1"}""");

        var unprocessed = _store.GetUnprocessedOutbox(10);
        Assert.Single(unprocessed);
        Assert.Null(unprocessed[0].ProcessedAt);

        _store.MarkOutboxProcessed("outbox-1");

        unprocessed = _store.GetUnprocessedOutbox(10);
        Assert.Empty(unprocessed);
    }

    [Fact]
    public void MarkOutboxFailed_TracksAttemptsAndError()
    {
        _store.InsertOutboxMessage("outbox-1", "matches", "match-1", "{}");
        _store.MarkOutboxFailed("outbox-1", "HTTP 500");

        var unprocessed = _store.GetUnprocessedOutbox(10);
        Assert.Single(unprocessed);
        Assert.Equal(1, unprocessed[0].Attempts);
        Assert.Equal("HTTP 500", unprocessed[0].LastError);
    }

    [Fact]
    public void GetUnprocessedOutbox_RespectLimit()
    {
        for (int i = 0; i < 5; i++)
            _store.InsertOutboxMessage($"msg-{i}", "matches", $"match-{i}", "{}");

        var batch = _store.GetUnprocessedOutbox(3);
        Assert.Equal(3, batch.Count);
    }
}
