using LearnPlayAI.Core.Models;
using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data.Tests;

public class SqliteVideoLibraryRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"learnplay-test-{Guid.NewGuid()}.db");

    [Fact]
    public async Task UpsertAndFindById_RoundTrips()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-1");

        await repo.UpsertAsync(entry);
        var found = await repo.FindByIdAsync(entry.VideoId);

        Assert.NotNull(found);
        Assert.Equal(entry.FilePath, found!.FilePath);
        Assert.Equal(entry.ContentHash, found.ContentHash);
        Assert.Equal(entry.Title, found.Title);
    }

    [Fact]
    public async Task FindByHash_ReturnsMatchingEntry()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-2");
        await repo.UpsertAsync(entry);

        var found = await repo.FindByHashAsync("hash-2");

        Assert.NotNull(found);
        Assert.Equal(entry.VideoId, found!.VideoId);
    }

    [Fact]
    public async Task FindByHash_NoMatch_ReturnsNull()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        Assert.Null(await repo.FindByHashAsync("does-not-exist"));
    }

    [Fact]
    public async Task UpdatePositionAsync_PersistsPositionAndWatchedTime()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-3");
        await repo.UpsertAsync(entry);

        var watchedUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await repo.UpdatePositionAsync(entry.VideoId, TimeSpan.FromMinutes(5), watchedUtc);

        var found = await repo.FindByIdAsync(entry.VideoId);
        Assert.Equal(TimeSpan.FromMinutes(5), found!.LastPosition);
        Assert.Equal(watchedUtc, found.LastWatchedUtc);
    }

    [Fact]
    public async Task UpdateDurationAsync_PersistsDuration()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-4");
        await repo.UpsertAsync(entry);

        await repo.UpdateDurationAsync(entry.VideoId, TimeSpan.FromMinutes(42));

        var found = await repo.FindByIdAsync(entry.VideoId);
        Assert.Equal(TimeSpan.FromMinutes(42), found!.Duration);
    }

    [Fact]
    public async Task GetRecentAsync_OrdersByLastWatchedDescending_AndExcludesNeverWatched()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var neverWatched = NewEntry(hash: "hash-5");
        var watchedOld = NewEntry(hash: "hash-6");
        var watchedNew = NewEntry(hash: "hash-7");
        await repo.UpsertAsync(neverWatched);
        await repo.UpsertAsync(watchedOld);
        await repo.UpsertAsync(watchedNew);

        await repo.UpdatePositionAsync(watchedOld.VideoId, TimeSpan.FromMinutes(1), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await repo.UpdatePositionAsync(watchedNew.VideoId, TimeSpan.FromMinutes(1), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var recent = await repo.GetRecentAsync(limit: 10);

        Assert.Equal(2, recent.Count);
        Assert.Equal(watchedNew.VideoId, recent[0].VideoId);
        Assert.Equal(watchedOld.VideoId, recent[1].VideoId);
    }

    [Fact]
    public async Task SetMissingAsync_PersistsFlag()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-8");
        await repo.UpsertAsync(entry);

        await repo.SetMissingAsync(entry.VideoId, true);

        var found = await repo.FindByIdAsync(entry.VideoId);
        Assert.True(found!.IsMissing);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-9");
        await repo.UpsertAsync(entry);

        await repo.DeleteAsync(entry.VideoId);

        Assert.Null(await repo.FindByIdAsync(entry.VideoId));
    }

    private static VideoEntry NewEntry(string hash) => new()
    {
        VideoId = Guid.NewGuid(),
        FilePath = @"C:\videos\sample.mp4",
        ContentHash = hash,
        Title = "Sample",
        AddedUtc = DateTime.UtcNow
    };

    public void Dispose()
    {
        // Release pooled SQLite handles so the file can be deleted on Windows.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
